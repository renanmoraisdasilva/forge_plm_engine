using System.Text.Json;
using ForgePlm.Api.Persistence;
using ForgePlm.Domain.Audit;
using ForgePlm.Domain.Common;
using ForgePlm.Domain.EngineeringChanges;
using Microsoft.EntityFrameworkCore;

namespace ForgePlm.Api.Application;

/// <summary>
/// Approve ECO — the ten steps of spec §8, in <b>one</b> transaction.
///
/// Any failure (a domain rule, a database constraint, a process crash before commit) leaves no
/// partial revision and no approval record. That is the single most important property of the
/// approval path: a half-created revision would be worse than a refused approval, because the
/// product history would then disagree with the audit trail.
///
/// Approval does <b>not</b> release the revision (spec §15.7). The new revision is `Approved`;
/// release is a separate, explicitly confirmed action.
/// </summary>
public sealed class ApproveEngineeringChange(ForgePlmDbContext db)
{
    /// <returns>The approved ECO, or `null` when it does not exist (404).</returns>
    public async Task<EngineeringChangeOrder?> ExecuteAsync(
        Guid ecoId,
        string? actor,
        string? comment,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // 1. Load the ECO and its lines.
        var eco = await db.EngineeringChangeOrders
            .Include(order => order.Lines)
            .FirstOrDefaultAsync(order => order.Id == ecoId, cancellationToken);

        if (eco is null)
        {
            return null;
        }

        var product = await db.Products
            .FirstOrDefaultAsync(p => p.Id == eco.ProductId, cancellationToken)
            ?? throw new DomainConflictException(
                DomainErrorCodes.StaleBaseRevision,
                $"ECO {eco.EcoNumber} references a product that no longer exists.");

        var baseRevision = await db.BomRevisions
            .Include(revision => revision.Lines)
            .FirstOrDefaultAsync(revision => revision.Id == eco.BaseReleasedRevisionId, cancellationToken)
            ?? throw new DomainConflictException(
                DomainErrorCodes.StaleBaseRevision,
                $"ECO {eco.EcoNumber} references a base revision that no longer exists.");

        // 2–3. Status and staleness. Both are checked before anything is mutated.
        var currentReleasedRevisionId = await EcoRules.CurrentReleasedRevisionIdAsync(
            db, eco.ProductId, cancellationToken);

        if (currentReleasedRevisionId is null)
        {
            throw new DomainConflictException(
                DomainErrorCodes.StaleBaseRevision,
                $"Product {product.PartNumber} has no current released revision; ECO {eco.EcoNumber} must be recreated.");
        }

        eco.EnsureBaseRevisionIsStillCurrent(currentReleasedRevisionId.Value);

        // 4. Replacement components still exist and are active.
        await EcoRules.EnsureReplacementsAreUsableAsync(db, eco.Lines, cancellationToken);

        var now = DateTime.UtcNow;

        // 5–7. Copy the base lines, apply the changes, create the new Approved revision linked
        //       to this ECO. The base revision itself is never touched.
        var nextRevisionCode = await EcoRules.NextRevisionCodeAsync(db, eco.ProductId, cancellationToken);
        var approvedRevision = eco.BuildApprovedRevision(
            baseRevision, nextRevisionCode, actor ?? string.Empty, now);

        db.BomRevisions.Add(approvedRevision);

        // 8. Record the decision and its link to the created revision.
        eco.Approve(approvedRevision.Id, actor ?? string.Empty, comment, now);

        // Load-and-write the product row so the xmin token participates in this transaction
        // (ADR-004). Without this, approval would write no product row and a concurrent release
        // could not invalidate it.
        product.MarkConfigurationChanged(now);

        // 9. Audit event, written in the same unit of work.
        db.AuditEvents.Add(AuditEvent.Record(
            eco.DecidedBy!,
            AuditActions.EcoApproved,
            "EngineeringChangeOrder",
            eco.Id,
            JsonSerializer.Serialize(new
            {
                ecoNumber = eco.EcoNumber,
                approvedRevisionId = approvedRevision.Id,
                revisionCode = approvedRevision.RevisionCode,
                productId = eco.ProductId,
            }),
            now));

        // 10. Commit all of it together.
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return eco;
    }
}
