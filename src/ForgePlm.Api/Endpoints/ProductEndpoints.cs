using ForgePlm.Api.Contracts;
using ForgePlm.Api.Persistence;
using ForgePlm.Domain.Products;
using Microsoft.EntityFrameworkCore;

namespace ForgePlm.Api.Endpoints;

/// <summary>Product reads (spec §7).</summary>
public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/products", ListAsync)
            .WithName("ListProducts")
            .WithSummary("Paged product list, optionally filtered by search term and lifecycle status.");

        app.MapGet("/api/products/{productId:guid}", GetAsync)
            .WithName("GetProduct")
            .WithSummary("One product, or 404.");

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

        var query = db.Products.AsNoTracking();
        query = ApplySearch(query, ListQuery.NormalisedTerm(search));
        query = ApplyStatus(query, ListQuery.ParseEnum<ProductLifecycleStatus>(status, "status"));

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(p => p.PartNumber)
            .Skip(paging.Skip)
            .Take(paging.EffectivePageSize)
            .Select(p => p.ToDto())
            .ToListAsync(cancellationToken);

        return Results.Ok(new PagedResponse<ProductDto>(items, total, paging.EffectivePage, paging.EffectivePageSize));
    }

    private static async Task<IResult> GetAsync(
        ForgePlmDbContext db,
        Guid productId,
        CancellationToken cancellationToken)
    {
        var product = await db.Products.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);

        return product is null
            ? Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Product not found.",
                detail: $"No product exists with id {productId}.")
            : Results.Ok(product.ToDto());
    }

    /// <summary>
    /// Case-insensitive substring match on part number and name.
    ///
    /// `ToLower().Contains()` rather than Npgsql's `ILike`, because `ILike` treats `%` and `_`
    /// in the user's input as wildcards — searching for `SERVER_100` would match `SERVER-100`.
    /// Escaping that correctly is more code than this needs, and ADR-001 rules out a second
    /// provider anyway, so there is nothing to buy by being provider-specific here.
    /// </summary>
    private static IQueryable<Product> ApplySearch(IQueryable<Product> query, string? term)
    {
        if (term is null)
        {
            return query;
        }

        var lowered = term.ToLowerInvariant();
        return query.Where(p =>
            p.PartNumber.ToLower().Contains(lowered) ||
            p.Name.ToLower().Contains(lowered));
    }

    private static IQueryable<Product> ApplyStatus(IQueryable<Product> query, ProductLifecycleStatus? status) =>
        status is null ? query : query.Where(p => p.LifecycleStatus == status);
}
