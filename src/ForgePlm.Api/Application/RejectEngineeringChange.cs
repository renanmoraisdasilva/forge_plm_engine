using System.Text.Json;
using ForgePlm.Api.Persistence;
using ForgePlm.Domain.Audit;
using ForgePlm.Domain.EngineeringChanges;
using Microsoft.EntityFrameworkCore;

namespace ForgePlm.Api.Application;

/// <summary>
/// Reject ECO (spec §8): submitted only, non-blank reason, reviewer and decision time recorded.
/// Rejection creates <b>no</b> BOM revision — the proposed configuration never existed, which is
/// the difference the rejection reason exists to explain.
/// </summary>
public sealed class RejectEngineeringChange(ForgePlmDbContext db)
{
    /// <returns>The rejected ECO, or `null` when it does not exist (404).</returns>
    public async Task<EngineeringChangeOrder?> ExecuteAsync(
        Guid ecoId,
        string? actor,
        string? rejectionReason,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var eco = await db.EngineeringChangeOrders
            .FirstOrDefaultAsync(order => order.Id == ecoId, cancellationToken);

        if (eco is null)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        eco.Reject(actor ?? string.Empty, rejectionReason, now);

        db.AuditEvents.Add(AuditEvent.Record(
            eco.DecidedBy!,
            AuditActions.EcoRejected,
            "EngineeringChangeOrder",
            eco.Id,
            JsonSerializer.Serialize(new
            {
                ecoNumber = eco.EcoNumber,
                reason = eco.ReviewerComment,
            }),
            now));

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return eco;
    }
}
