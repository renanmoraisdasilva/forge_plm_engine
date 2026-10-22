using ForgePlm.Api.Contracts;
using ForgePlm.Api.Persistence;
using ForgePlm.Domain.EngineeringChanges;
using Microsoft.EntityFrameworkCore;

namespace ForgePlm.Api.Application;

/// <summary>
/// Read projections for the ECO screens (spec §7). Never returns tracked entities: these run
/// after writes on the same context, and handing the client a tracked aggregate would expose
/// its internal line collection.
/// </summary>
public static class EcoQueries
{
    public static async Task<PagedResponse<EcoSummaryDto>> ListAsync(
        ForgePlmDbContext db,
        string? search,
        EngineeringChangeStatus? status,
        Guid? productId,
        PageRequest paging,
        CancellationToken cancellationToken = default)
    {
        var query = db.EngineeringChangeOrders.AsNoTracking();

        if (status is not null)
        {
            query = query.Where(order => order.Status == status);
        }

        if (productId is not null)
        {
            query = query.Where(order => order.ProductId == productId);
        }

        if (search is not null)
        {
            // Same reasoning as the product list: `ToLower().Contains()` rather than `ILike`,
            // so `%` and `_` in the user's input are not treated as wildcards.
            var lowered = search.ToLowerInvariant();
            query = query.Where(order =>
                order.EcoNumber.ToLower().Contains(lowered) ||
                order.Title.ToLower().Contains(lowered));
        }

        var total = await query.CountAsync(cancellationToken);

        // Status is projected as the enum and stringified in memory: EF Core cannot translate
        // `ToString()` inside a SQL projection, and the enum is stored as a string anyway.
        var rows = await query
            .OrderByDescending(order => order.CreatedAtUtc)
            .ThenByDescending(order => order.EcoNumber)
            .Skip(paging.Skip)
            .Take(paging.EffectivePageSize)
            .Select(order => new
            {
                order.Id,
                order.EcoNumber,
                order.Title,
                order.ProductId,
                Product = db.Products
                    .Where(product => product.Id == order.ProductId)
                    .Select(product => new EcoProductDto(product.Id, product.PartNumber, product.Name))
                    .First(),
                order.Status,
                order.CreatedBy,
                order.CreatedAtUtc,
                LineCount = order.Lines.Count,
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(row => new EcoSummaryDto(
                row.Id,
                row.EcoNumber,
                row.Title,
                row.ProductId,
                row.Product,
                row.Status.ToString(),
                row.CreatedBy,
                row.CreatedAtUtc,
                row.LineCount))
            .ToList();

        return new PagedResponse<EcoSummaryDto>(items, total, paging.EffectivePage, paging.EffectivePageSize);
    }

    /// <summary>
    /// The detail screen's one call: header plus lines with their target and replacement
    /// component summaries denormalised in.
    /// </summary>
    public static async Task<EcoDetailDto?> GetDetailAsync(
        ForgePlmDbContext db,
        Guid ecoId,
        CancellationToken cancellationToken = default)
    {
        var eco = await db.EngineeringChangeOrders.AsNoTracking()
            .Include(order => order.Lines)
            .FirstOrDefaultAsync(order => order.Id == ecoId, cancellationToken);

        if (eco is null)
        {
            return null;
        }

        var product = await db.Products.AsNoTracking()
            .Where(candidate => candidate.Id == eco.ProductId)
            .Select(candidate => new EcoProductDto(candidate.Id, candidate.PartNumber, candidate.Name))
            .FirstAsync(cancellationToken);

        var baseRevisionCode = await db.BomRevisions.AsNoTracking()
            .Where(revision => revision.Id == eco.BaseReleasedRevisionId)
            .Select(revision => revision.RevisionCode)
            .FirstAsync(cancellationToken);

        var componentIds = eco.Lines
            .SelectMany(line => new[] { (Guid?)line.TargetComponentId, line.ReplacementComponentId })
            .OfType<Guid>()
            .Distinct()
            .ToList();

        var components = await db.Components.AsNoTracking()
            .Where(component => componentIds.Contains(component.Id))
            .Select(component => new ComponentRefDto(component.Id, component.PartNumber, component.Name))
            .ToListAsync(cancellationToken);

        var componentsById = components.ToDictionary(component => component.Id);

        var lines = eco.Lines
            .Select(line => new EcoLineDto(
                line.Id,
                line.ChangeType.ToString(),
                line.TargetComponentId,
                componentsById[line.TargetComponentId],
                line.ReplacementComponentId,
                line.ReplacementComponentId is { } replacementId ? componentsById[replacementId] : null,
                line.NewQuantity,
                string.IsNullOrWhiteSpace(line.Reason) ? null : line.Reason))
            .ToList();

        return new EcoDetailDto(
            eco.Id,
            eco.EcoNumber,
            eco.Title,
            eco.ProductId,
            product,
            eco.Status.ToString(),
            eco.CreatedBy,
            eco.CreatedAtUtc,
            lines.Count,
            eco.BaseReleasedRevisionId,
            baseRevisionCode,
            eco.ReasonForChange,
            eco.ImpactAssessment,
            eco.RequestedEffectiveAtUtc,
            eco.SubmittedAtUtc,
            eco.DecidedAtUtc,
            eco.DecidedBy,
            eco.ReviewerComment,
            eco.ApprovedRevisionId,
            lines);
    }
}
