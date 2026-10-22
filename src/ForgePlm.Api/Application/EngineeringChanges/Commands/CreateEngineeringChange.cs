using System.Text.Json;
using ForgePlm.Api.Persistence;
using ForgePlm.Domain.Audit;
using ForgePlm.Domain.BillOfMaterials;
using ForgePlm.Domain.Common;
using ForgePlm.Domain.EngineeringChanges;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ForgePlm.Api.Application;

/// <summary>
/// One proposed change line as the API receives it. Kept separate from the API contract so the
/// use cases can be driven directly by integration tests without constructing HTTP payloads.
/// </summary>
public sealed record NewEcoLine(
    EngineeringChangeType ChangeType,
    Guid TargetComponentId,
    Guid? ReplacementComponentId,
    int? NewQuantity,
    string? Reason);

/// <summary>Everything needed to open a draft. Base revision and ECO number are resolved server-side.</summary>
public sealed record NewEco(
    Guid ProductId,
    string? Title,
    string? ReasonForChange,
    string? ImpactAssessment,
    DateTime? RequestedEffectiveAtUtc,
    string? Actor,
    IReadOnlyList<NewEcoLine>? Lines);

/// <summary>
/// Create ECO (spec §8).
///
/// Two contract points are load-bearing here:
///
///   * `baseReleasedRevisionId` is resolved from the product's <b>current</b> released revision
///     at creation time. It is not a request field, so a stale client cannot propose a change
///     against a superseded revision — spec §11's IT-01 is true by construction.
///   * the ECO number is generated server-side and creation retries on a unique violation.
///     The database index, not the generator, is the guarantee that a number is never reused.
/// </summary>
public sealed class CreateEngineeringChange(ForgePlmDbContext db)
{
    /// <summary>
    /// Bounded so a pathological collision cannot loop forever. Each retry re-reads the highest
    /// number for the year, so a collision means someone else committed the same number between
    /// the read and the insert — the next attempt sees it.
    /// </summary>
    public const int MaxNumberAttempts = 5;

    /// <returns>The created ECO, or `null` when the product does not exist (404).</returns>
    public async Task<EngineeringChangeOrder?> ExecuteAsync(
        NewEco command,
        CancellationToken cancellationToken = default)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == command.ProductId, cancellationToken);
        if (product is null)
        {
            return null;
        }

        product.EnsureIsActiveForChangeProposal();

        var now = DateTime.UtcNow;
        var baseRevision = await db.BomRevisions.AsNoTracking()
            .FirstOrDefaultAsync(
                revision => revision.ProductId == product.Id && revision.Status == BomRevisionStatus.Released &&
                            revision.EffectiveAtUtc.HasValue && revision.EffectiveAtUtc.Value <= now,
                cancellationToken)
            ?? throw new DomainConflictException(
                DomainErrorCodes.NoReleasedRevision,
                $"Product {product.PartNumber} has no released BOM revision, so there is nothing to change.");

        var requestedEffectiveAtUtc = UtcTimestamp.Normalise(command.RequestedEffectiveAtUtc);

        for (var attempt = 1; ; attempt++)
        {
            var ecoNumber = await EcoNumberGenerator.NextAsync(db, now, cancellationToken);

            var eco = EngineeringChangeOrder.Create(
                ecoNumber,
                product.Id,
                baseRevision.Id,
                command.Title,
                command.ReasonForChange,
                command.ImpactAssessment,
                requestedEffectiveAtUtc,
                command.Actor ?? string.Empty,
                now);

            eco.ReplaceLines(EcoLineFactory.Build(command.Lines));

            db.EngineeringChangeOrders.Add(eco);

            // The audit row is added before the same SaveChanges, so it is committed with the
            // ECO or not at all (spec §5.5).
            db.AuditEvents.Add(AuditEvent.Record(
                eco.CreatedBy,
                AuditActions.EcoCreated,
                "EngineeringChangeOrder",
                eco.Id,
                JsonSerializer.Serialize(new
                {
                    ecoNumber = eco.EcoNumber,
                    productId = eco.ProductId,
                    baseReleasedRevisionId = eco.BaseReleasedRevisionId,
                    lineCount = eco.Lines.Count,
                }),
                now));

            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return eco;
            }
            catch (DbUpdateException ex) when (attempt < MaxNumberAttempts && IsEcoNumberCollision(ex))
            {
                // The insert failed on the ECO-number index. Nothing else in this unit of work
                // may be kept: detach so the next attempt starts from a clean tracker.
                db.ChangeTracker.Clear();
            }
        }
    }

    private static bool IsEcoNumberCollision(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "ux_ecos_eco_number",
        };
}
