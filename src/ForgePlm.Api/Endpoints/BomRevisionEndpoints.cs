using ForgePlm.Api.Contracts;
using ForgePlm.Api.Persistence;
using ForgePlm.Domain.BillOfMaterials;
using Microsoft.EntityFrameworkCore;

namespace ForgePlm.Api.Endpoints;

/// <summary>BOM revision reads (spec §7).</summary>
public static class BomRevisionEndpoints
{
    public static IEndpointRouteBuilder MapBomRevisionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/products/{productId:guid}/bom-revisions", ListForProductAsync)
            .WithName("ListBomRevisions")
            .WithSummary("Paged revision history for one product, newest first.");

        app.MapGet("/api/products/{productId:guid}/bom-revisions/current", GetCurrentAsync)
            .WithName("GetCurrentBomRevision")
            .WithSummary("The product's single Released revision, with its lines. 404 if none is released.");

        app.MapGet("/api/bom-revisions/{revisionId:guid}", GetAsync)
            .WithName("GetBomRevision")
            .WithSummary("One revision header, or 404.");

        app.MapGet("/api/bom-revisions/{revisionId:guid}/lines", GetLinesAsync)
            .WithName("ListBomRevisionLines")
            .WithSummary("Paged lines of one revision, each carrying its component summary.");

        return app;
    }

    private static async Task<IResult> ListForProductAsync(
        ForgePlmDbContext db,
        Guid productId,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken)
    {
        if (!await ProductExistsAsync(db, productId, cancellationToken))
        {
            return NotFoundProduct(productId);
        }

        var paging = PageRequest.From(page, pageSize);

        var query = db.BomRevisions.AsNoTracking().Where(r => r.ProductId == productId);
        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(r => r.CreatedAtUtc)
            .ThenByDescending(r => r.RevisionCode)
            .Skip(paging.Skip)
            .Take(paging.EffectivePageSize)
            .Select(r => new
            {
                Revision = r,
                LineCount = r.Lines.Count,
            })
            .ToListAsync(cancellationToken);

        var dto = items.Select(x => x.Revision.ToDto(x.LineCount)).ToList();

        return Results.Ok(new PagedResponse<BomRevisionDto>(dto, total, paging.EffectivePage, paging.EffectivePageSize));
    }

    /// <summary>
    /// The product's current configuration — the one `Released` revision, which the schema
    /// guarantees is unique per product (ADR-004's partial index).
    ///
    /// 404 rather than an empty body when nothing is released: "this product has no current
    /// revision" is a real state the UI must render, and a 200 with nulls would make the client
    /// guess whether the request failed.
    /// </summary>
    private static async Task<IResult> GetCurrentAsync(
        ForgePlmDbContext db,
        Guid productId,
        CancellationToken cancellationToken)
    {
        if (!await ProductExistsAsync(db, productId, cancellationToken))
        {
            return NotFoundProduct(productId);
        }

        var revision = await db.BomRevisions.AsNoTracking()
            .Where(r => r.ProductId == productId && r.Status == BomRevisionStatus.Released)
            .FirstOrDefaultAsync(cancellationToken);

        if (revision is null)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "No released revision.",
                detail: $"Product {productId} has no released BOM revision.");
        }

        var lines = await db.BomLines.AsNoTracking()
            .Where(l => l.BomRevisionId == revision.Id)
            .Join(
                db.Components.AsNoTracking(),
                line => line.ComponentId,
                component => component.Id,
                (line, component) => new { Line = line, Component = component })
            .OrderBy(x => x.Component.PartNumber)
            .Select(x => x.Line.ToDto(x.Component))
            .ToListAsync(cancellationToken);

        return Results.Ok(new CurrentRevisionResponse(revision.ToDto(lines.Count), lines));
    }

    private static async Task<IResult> GetAsync(
        ForgePlmDbContext db,
        Guid revisionId,
        CancellationToken cancellationToken)
    {
        var query = db.BomRevisions.AsNoTracking().Where(r => r.Id == revisionId);

        var header = await query
            .Select(r => new { Revision = r, LineCount = r.Lines.Count })
            .FirstOrDefaultAsync(cancellationToken);

        return header is null ? NotFoundRevision(revisionId) : Results.Ok(header.Revision.ToDto(header.LineCount));
    }

    private static async Task<IResult> GetLinesAsync(
        ForgePlmDbContext db,
        Guid revisionId,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken)
    {
        if (!await db.BomRevisions.AsNoTracking().AnyAsync(r => r.Id == revisionId, cancellationToken))
        {
            return NotFoundRevision(revisionId);
        }

        var paging = PageRequest.From(page, pageSize);

        var query = db.BomLines.AsNoTracking().Where(l => l.BomRevisionId == revisionId);
        var total = await query.CountAsync(cancellationToken);

        var lines = await query
            .Join(
                db.Components.AsNoTracking(),
                line => line.ComponentId,
                component => component.Id,
                (line, component) => new { Line = line, Component = component })
            .OrderBy(x => x.Component.PartNumber)
            .Skip(paging.Skip)
            .Take(paging.EffectivePageSize)
            .Select(x => x.Line.ToDto(x.Component))
            .ToListAsync(cancellationToken);

        return Results.Ok(new PagedResponse<BomLineDto>(lines, total, paging.EffectivePage, paging.EffectivePageSize));
    }

    private static Task<bool> ProductExistsAsync(ForgePlmDbContext db, Guid productId, CancellationToken ct) =>
        db.Products.AsNoTracking().AnyAsync(p => p.Id == productId, ct);

    private static IResult NotFoundProduct(Guid productId) => Results.Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Product not found.",
        detail: $"No product exists with id {productId}.");

    private static IResult NotFoundRevision(Guid revisionId) => Results.Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "BOM revision not found.",
        detail: $"No BOM revision exists with id {revisionId}.");
}
