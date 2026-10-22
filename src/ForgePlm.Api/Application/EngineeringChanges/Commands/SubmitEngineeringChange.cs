using System.Text.Json;
using ForgePlm.Api.Persistence;
using ForgePlm.Domain.Audit;
using ForgePlm.Domain.Common;
using ForgePlm.Domain.EngineeringChanges;
using Microsoft.EntityFrameworkCore;

namespace ForgePlm.Api.Application;

/// <summary>
/// Submit ECO (spec §8): draft only, at least one line, every target present in the base
/// revision, every replacement existing and active. The domain owns the first two and the
/// target check; the application service owns the component lookup.
/// </summary>
public sealed class SubmitEngineeringChange(ForgePlmDbContext db)
{
    /// <returns>The submitted ECO, or `null` when it does not exist (404).</returns>
    public async Task<EngineeringChangeOrder?> ExecuteAsync(
        Guid ecoId,
        string? actor,
        CancellationToken cancellationToken = default)
    {
        var eco = await db.EngineeringChangeOrders
            .Include(order => order.Lines)
            .FirstOrDefaultAsync(order => order.Id == ecoId, cancellationToken);

        if (eco is null)
        {
            return null;
        }

        var baseRevision = await db.BomRevisions
            .Include(revision => revision.Lines)
            .FirstOrDefaultAsync(revision => revision.Id == eco.BaseReleasedRevisionId, cancellationToken)
            ?? throw new DomainConflictException(
                DomainErrorCodes.StaleBaseRevision,
                $"ECO {eco.EcoNumber} references a base revision that no longer exists.");

        await EcoRules.EnsureReplacementsAreUsableAsync(db, eco.Lines, cancellationToken);

        var now = DateTime.UtcNow;
        eco.Submit(baseRevision, now);

        db.AuditEvents.Add(AuditEvent.Record(
            actor ?? string.Empty,
            AuditActions.EcoSubmitted,
            "EngineeringChangeOrder",
            eco.Id,
            JsonSerializer.Serialize(new { ecoNumber = eco.EcoNumber, lineCount = eco.Lines.Count }),
            now));

        await db.SaveChangesAsync(cancellationToken);

        return eco;
    }
}
