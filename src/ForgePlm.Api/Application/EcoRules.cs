using ForgePlm.Api.Persistence;
using ForgePlm.Domain.BillOfMaterials;
using ForgePlm.Domain.Common;
using ForgePlm.Domain.EngineeringChanges;
using Microsoft.EntityFrameworkCore;

namespace ForgePlm.Api.Application;

/// <summary>
/// Cross-aggregate checks shared by submit, approve and release.
///
/// These need `Component` and `BomRevision` rows, which the domain aggregates deliberately
/// cannot see (spec §5.4). Keeping them here means the domain checks everything decidable from
/// the ECO and its base revision, and the application service checks the rest — one place each.
/// </summary>
internal static class EcoRules
{
    public static async Task<Guid?> CurrentReleasedRevisionIdAsync(
        ForgePlmDbContext db,
        Guid productId,
        CancellationToken cancellationToken) =>
        await db.BomRevisions.AsNoTracking()
            .Where(r => r.ProductId == productId && r.Status == BomRevisionStatus.Released)
            .Select(r => (Guid?)r.Id)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Spec §8: every replacement must exist and be active before an ECO is submitted or
    /// approved. Retired parts stay referenced by historical revisions, but may not enter a
    /// new one (§5.2).
    /// </summary>
    public static async Task EnsureReplacementsAreUsableAsync(
        ForgePlmDbContext db,
        IEnumerable<EngineeringChangeLine> lines,
        CancellationToken cancellationToken)
    {
        var replacementIds = lines
            .Where(line => line.ChangeType == EngineeringChangeType.ReplaceComponent)
            .Select(line => line.ReplacementComponentId)
            .OfType<Guid>()
            .Distinct()
            .ToList();

        if (replacementIds.Count == 0)
        {
            return;
        }

        var components = await db.Components.AsNoTracking()
            .Where(component => replacementIds.Contains(component.Id))
            .ToListAsync(cancellationToken);

        var byId = components.ToDictionary(component => component.Id);

        foreach (var id in replacementIds)
        {
            if (!byId.TryGetValue(id, out var component))
            {
                throw new DomainRuleViolationException(
                    DomainErrorCodes.ReplacementComponentRequired,
                    $"Replacement component {id} does not exist.");
            }

            component.EnsureCanBeUsedInNewBom();
        }
    }

    /// <summary>
    /// The next revision code for a product. Derived from the highest existing code, not from
    /// the base revision alone: an ECO may already have produced an `Approved` revision that
    /// has not been released yet, and two revisions of one product cannot share a code (§5.3).
    /// </summary>
    public static async Task<string> NextRevisionCodeAsync(
        ForgePlmDbContext db,
        Guid productId,
        CancellationToken cancellationToken)
    {
        var codes = await db.BomRevisions.AsNoTracking()
            .Where(revision => revision.ProductId == productId)
            .Select(revision => revision.RevisionCode)
            .ToListAsync(cancellationToken);

        if (codes.Count == 0)
        {
            return "A";
        }

        return RevisionCode.Next(codes.Max(StringComparer.Ordinal));
    }
}
