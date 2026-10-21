using ForgePlm.Api.Contracts;
using ForgePlm.Api.Persistence;
using ForgePlm.Domain.Components;
using Microsoft.EntityFrameworkCore;

namespace ForgePlm.Api.Endpoints;

/// <summary>
/// Component reads (spec §7).
///
/// Mandatory even though there is no Components screen: the Create/Edit ECO form needs a
/// component picker to choose replacement parts.
/// </summary>
public static class ComponentEndpoints
{
    public static IEndpointRouteBuilder MapComponentEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/components", ListAsync)
            .WithName("ListComponents")
            .WithSummary("Paged component list, optionally filtered by search term and lifecycle status.");

        app.MapGet("/api/components/{componentId:guid}", GetAsync)
            .WithName("GetComponent")
            .WithSummary("One component, or 404.");

        return app;
    }

    /// <param name="search">Matches part number or name, case-insensitively.</param>
    /// <param name="status">Lifecycle status; an unrecognised value is a 400, not an empty list.</param>
    private static async Task<IResult> ListAsync(
        ForgePlmDbContext db,
        string? search,
        string? status,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken)
    {
        var paging = PageRequest.From(page, pageSize);

        var query = db.Components.AsNoTracking();
        query = ApplySearch(query, ListQuery.NormalisedTerm(search));
        query = ApplyStatus(query, ListQuery.ParseEnum<ComponentLifecycleStatus>(status, "status"));

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(c => c.PartNumber)
            .Skip(paging.Skip)
            .Take(paging.EffectivePageSize)
            .Select(c => c.ToDto())
            .ToListAsync(cancellationToken);

        return Results.Ok(new PagedResponse<ComponentDto>(items, total, paging.EffectivePage, paging.EffectivePageSize));
    }

    private static async Task<IResult> GetAsync(
        ForgePlmDbContext db,
        Guid componentId,
        CancellationToken cancellationToken)
    {
        var component = await db.Components.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == componentId, cancellationToken);

        return component is null
            ? Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Component not found.",
                detail: $"No component exists with id {componentId}.")
            : Results.Ok(component.ToDto());
    }

    /// <summary>Same rationale as the product search — see <c>ProductEndpoints</c>.</summary>
    private static IQueryable<Component> ApplySearch(IQueryable<Component> query, string? term)
    {
        if (term is null)
        {
            return query;
        }

        var lowered = term.ToLowerInvariant();
        return query.Where(c =>
            c.PartNumber.ToLower().Contains(lowered) ||
            c.Name.ToLower().Contains(lowered));
    }

    private static IQueryable<Component> ApplyStatus(IQueryable<Component> query, ComponentLifecycleStatus? status) =>
        status is null ? query : query.Where(c => c.LifecycleStatus == status);
}
