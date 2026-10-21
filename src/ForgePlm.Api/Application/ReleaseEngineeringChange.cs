using System.Text.Json;
using ForgePlm.Api.Persistence;
using ForgePlm.Domain.Audit;
using ForgePlm.Domain.BillOfMaterials;
using ForgePlm.Domain.Common;
using ForgePlm.Domain.EngineeringChanges;
using Microsoft.EntityFrameworkCore;

namespace ForgePlm.Api.Application;

/// <summary>
/// Release ECO (spec §8 and §15.5; concurrency details in ADR-004).
///
/// <para>
/// <b>Ordering is load-bearing.</b> `ux_bom_revisions_one_released_per_product` is a partial
/// unique index, so it is checked per statement and PostgreSQL cannot defer it (DEFERRABLE is a
/// table-constraint feature; a partial index is not one). The old revision must be superseded
/// <i>before</i> the new one is released. A single batched `SaveChanges` happens not to fail
/// today — measured, not assumed — but EF Core guarantees ordering only for dependent commands,
/// so this sequences two statements inside one explicit transaction:
/// </para>
///
/// <code>
/// supersede -&gt; save -&gt; release -&gt; save -&gt; commit
/// </code>
///
/// <para>
/// Release takes effect immediately. `EffectiveAtUtc` is a recorded label: a future value is
/// stored and displayed as "Scheduled", and nothing may filter on it to decide what is current
/// (spec §15.5). There is no scheduler.
/// </para>
/// </summary>
public sealed class ReleaseEngineeringChange(ForgePlmDbContext db)
{
    /// <returns>The applied ECO, or `null` when it does not exist (404).</returns>
    public async Task<EngineeringChangeOrder?> ExecuteAsync(
        Guid ecoId,
        string? actor,
        DateTime? effectiveAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(actor))
        {
            throw new DomainRuleViolationException(
                DomainErrorCodes.BlankActor,
                "An actor is required for this action.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var eco = await db.EngineeringChangeOrders
            .FirstOrDefaultAsync(order => order.Id == ecoId, cancellationToken);

        if (eco is null)
        {
            return null;
        }

        // 1–3. Only an approved ECO with a linked approved revision may be released.
        if (eco.Status != EngineeringChangeStatus.Approved || eco.ApprovedRevisionId is null)
        {
            throw new DomainConflictException(
                DomainErrorCodes.InvalidTransition,
                $"ECO {eco.EcoNumber} is {eco.Status}; only an approved ECO can be released.");
        }

        // 5. An effective date is required, but it is a label, not a trigger.
        if (effectiveAtUtc is null)
        {
            throw new DomainRuleViolationException(
                DomainErrorCodes.EffectiveDateRequired,
                "Release requires an effective date. It is recorded immediately; a future date is shown as scheduled.");
        }

        var effectiveAt = UtcTimestamp.Normalise(effectiveAtUtc.Value);

        var product = await db.Products
            .FirstOrDefaultAsync(p => p.Id == eco.ProductId, cancellationToken)
            ?? throw new DomainConflictException(
                DomainErrorCodes.StaleBaseRevision,
                $"ECO {eco.EcoNumber} references a product that no longer exists.");

        product.EnsureCanReceiveNewlyReleasedRevision();

        var approvedRevision = await db.BomRevisions
            .FirstOrDefaultAsync(revision => revision.Id == eco.ApprovedRevisionId, cancellationToken)
            ?? throw new DomainConflictException(
                DomainErrorCodes.InvalidTransition,
                $"ECO {eco.EcoNumber} links to a revision that no longer exists.");

        if (approvedRevision.ProductId != eco.ProductId || approvedRevision.SourceEcoId != eco.Id)
        {
            throw new DomainConflictException(
                DomainErrorCodes.InvalidTransition,
                $"ECO {eco.EcoNumber} may only release the revision approval created for it.");
        }

        // 4. The base revision must still be the product's current released revision. If a
        //    competing ECO released first, this is the stale-base conflict — never a silent
        //    overwrite of the newer configuration.
        var currentReleased = await db.BomRevisions
            .FirstOrDefaultAsync(
                revision => revision.ProductId == eco.ProductId && revision.Status == BomRevisionStatus.Released,
                cancellationToken)
            ?? throw new DomainConflictException(
                DomainErrorCodes.StaleBaseRevision,
                $"Product {product.PartNumber} has no current released revision.");

        eco.EnsureBaseRevisionIsStillCurrent(currentReleased.Id);

        var now = DateTime.UtcNow;

        // Load-and-write the product row so the xmin token participates (ADR-004), and maintain
        // UpdatedAtUtc while doing so.
        product.MarkConfigurationChanged(now);

        // 6. Supersede the prior current revision, and persist that before releasing the new one.
        currentReleased.MarkSuperseded();
        await db.SaveChangesAsync(cancellationToken);

        // 7–8. Release the new revision and mark the ECO applied.
        approvedRevision.Release(effectiveAt);
        eco.MarkApplied(now);

        // 9. Audit event in the same transaction.
        db.AuditEvents.Add(AuditEvent.Record(
            actor,
            AuditActions.RevisionReleased,
            "EngineeringChangeOrder",
            eco.Id,
            JsonSerializer.Serialize(new
            {
                ecoNumber = eco.EcoNumber,
                revisionId = approvedRevision.Id,
                revisionCode = approvedRevision.RevisionCode,
                effectiveAtUtc = effectiveAt,
                supersededRevisionId = currentReleased.Id,
            }),
            now));

        // 10. Commit both statements together. A failure here rolls the supersede back, so the
        //     product is never left with no current released revision.
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return eco;
    }
}
