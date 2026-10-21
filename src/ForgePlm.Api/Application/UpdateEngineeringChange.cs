using ForgePlm.Api.Persistence;
using ForgePlm.Domain.EngineeringChanges;
using Microsoft.EntityFrameworkCore;

namespace ForgePlm.Api.Application;

/// <summary>
/// Edit a draft ECO (spec §7: draft only). A submitted, approved, rejected or applied ECO is
/// immutable in this prototype, and the domain's `UpdateDraft` raises `invalid-transition`.
/// </summary>
public sealed class UpdateEngineeringChange(ForgePlmDbContext db)
{
    /// <returns>The updated ECO, or `null` when it does not exist (404).</returns>
    public async Task<EngineeringChangeOrder?> ExecuteAsync(
        Guid ecoId,
        EcoEdit edit,
        CancellationToken cancellationToken = default)
    {
        var eco = await db.EngineeringChangeOrders
            .Include(order => order.Lines)
            .FirstOrDefaultAsync(order => order.Id == ecoId, cancellationToken);

        if (eco is null)
        {
            return null;
        }

        // Capture the lines before the domain clears the collection. EF Core's change
        // detection discovers a *new* entity added to a tracked navigation with an
        // application-assigned key as `Modified` (it assumes an entity with a key already
        // exists), which produces an UPDATE that matches no row. Replace-all therefore states
        // the tracking intent explicitly: these rows go, these rows arrive.
        var replacedLines = eco.Lines.ToList();
        var newLines = EcoLineFactory.Build(edit.Lines);

        eco.UpdateDraft(
            edit.Title,
            edit.ReasonForChange,
            edit.ImpactAssessment,
            UtcTimestamp.Normalise(edit.RequestedEffectiveAtUtc),
            newLines);

        db.EngineeringChangeLines.RemoveRange(replacedLines);
        db.EngineeringChangeLines.AddRange(newLines);

        await db.SaveChangesAsync(cancellationToken);

        return eco;
    }
}
