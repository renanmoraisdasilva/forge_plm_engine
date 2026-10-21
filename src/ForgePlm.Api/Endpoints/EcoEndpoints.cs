using ForgePlm.Api.Application;
using ForgePlm.Api.Contracts;
using ForgePlm.Api.Persistence;
using ForgePlm.Domain.EngineeringChanges;

namespace ForgePlm.Api.Endpoints;

/// <summary>
/// Engineering-change write and read endpoints (spec §7, `docs/ECO-API-CONTRACT.md`).
///
/// Handlers stay thin: bind, delegate to one use case, return a DTO. They deliberately do not
/// catch exceptions — the registered <see cref="Http.ForgePlmExceptionHandler"/> owns the
/// mapping, so a new endpoint cannot return a different status for the same failure.
/// </summary>
public static class EcoEndpoints
{
    public static IEndpointRouteBuilder MapEcoEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/ecos", ListAsync)
            .WithName("ListEcos")
            .WithSummary("Paged ECO list, filtered by search term, status and product.");

        app.MapGet("/api/ecos/{ecoId:guid}", GetAsync)
            .WithName("GetEco")
            .WithSummary("One ECO with its change lines, or 404.");

        app.MapPost("/api/ecos", CreateAsync)
            .WithName("CreateEco")
            .WithSummary("Create a draft ECO. Number and base released revision are server-resolved.");

        app.MapPut("/api/ecos/{ecoId:guid}", UpdateAsync)
            .WithName("UpdateEco")
            .WithSummary("Edit a draft ECO. Any other status is 409 invalid-transition.");

        app.MapPost("/api/ecos/{ecoId:guid}/submit", SubmitAsync)
            .WithName("SubmitEco")
            .WithSummary("Draft -> Submitted, after validating targets and replacements.");

        app.MapPost("/api/ecos/{ecoId:guid}/approve", ApproveAsync)
            .WithName("ApproveEco")
            .WithSummary("The ten-step approval transaction. Creates an Approved revision, never releases it.");

        app.MapPost("/api/ecos/{ecoId:guid}/reject", RejectAsync)
            .WithName("RejectEco")
            .WithSummary("Submitted -> Rejected with a non-blank reason. Creates no BOM revision.");

        app.MapPost("/api/ecos/{ecoId:guid}/release", ReleaseAsync)
            .WithName("ReleaseEco")
            .WithSummary("Approved -> Applied. Supersedes the old revision and releases the new one atomically.");

        return app;
    }

    private static async Task<IResult> ListAsync(
        ForgePlmDbContext db,
        string? search,
        string? status,
        Guid? productId,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken)
    {
        var paging = PageRequest.From(page, pageSize);

        var result = await EcoQueries.ListAsync(
            db,
            ListQuery.NormalisedTerm(search),
            ListQuery.ParseEnum<EngineeringChangeStatus>(status, "status"),
            productId,
            paging,
            cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetAsync(
        ForgePlmDbContext db,
        Guid ecoId,
        CancellationToken cancellationToken)
    {
        var detail = await EcoQueries.GetDetailAsync(db, ecoId, cancellationToken);

        return detail is null ? NotFoundEco(ecoId) : Results.Ok(detail);
    }

    private static async Task<IResult> CreateAsync(
        ForgePlmDbContext db,
        CreateEngineeringChange useCase,
        CreateEcoRequest request,
        CancellationToken cancellationToken)
    {
        var eco = await useCase.ExecuteAsync(
            new NewEco(
                request.ProductId,
                request.Title,
                request.ReasonForChange,
                request.ImpactAssessment,
                request.RequestedEffectiveAtUtc,
                request.Actor,
                ToLineCommands(request.Lines)),
            cancellationToken);

        if (eco is null)
        {
            return NotFoundProduct(request.ProductId);
        }

        var detail = await EcoQueries.GetDetailAsync(db, eco.Id, cancellationToken);

        return Results.Created($"/api/ecos/{eco.Id}", detail);
    }

    private static async Task<IResult> UpdateAsync(
        ForgePlmDbContext db,
        UpdateEngineeringChange useCase,
        Guid ecoId,
        UpdateEcoRequest request,
        CancellationToken cancellationToken)
    {
        var eco = await useCase.ExecuteAsync(
            ecoId,
            new EcoEdit(
                request.Title,
                request.ReasonForChange,
                request.ImpactAssessment,
                request.RequestedEffectiveAtUtc,
                ToLineCommands(request.Lines)),
            cancellationToken);

        return await ResultAsync(db, eco, ecoId, cancellationToken);
    }

    private static async Task<IResult> SubmitAsync(
        ForgePlmDbContext db,
        SubmitEngineeringChange useCase,
        Guid ecoId,
        ActorRequest request,
        CancellationToken cancellationToken)
    {
        var eco = await useCase.ExecuteAsync(ecoId, request.Actor, cancellationToken);

        return await ResultAsync(db, eco, ecoId, cancellationToken);
    }

    private static async Task<IResult> ApproveAsync(
        ForgePlmDbContext db,
        ApproveEngineeringChange useCase,
        Guid ecoId,
        ApproveEcoRequest request,
        CancellationToken cancellationToken)
    {
        var eco = await useCase.ExecuteAsync(ecoId, request.Actor, request.Comment, cancellationToken);

        return await ResultAsync(db, eco, ecoId, cancellationToken);
    }

    private static async Task<IResult> RejectAsync(
        ForgePlmDbContext db,
        RejectEngineeringChange useCase,
        Guid ecoId,
        RejectEcoRequest request,
        CancellationToken cancellationToken)
    {
        var eco = await useCase.ExecuteAsync(ecoId, request.Actor, request.RejectionReason, cancellationToken);

        return await ResultAsync(db, eco, ecoId, cancellationToken);
    }

    private static async Task<IResult> ReleaseAsync(
        ForgePlmDbContext db,
        ReleaseEngineeringChange useCase,
        Guid ecoId,
        ReleaseEcoRequest request,
        CancellationToken cancellationToken)
    {
        var eco = await useCase.ExecuteAsync(ecoId, request.Actor, request.EffectiveAtUtc, cancellationToken);

        return await ResultAsync(db, eco, ecoId, cancellationToken);
    }

    /// <summary>Every action returns the same detail shape, so the ECO screen re-renders from one response.</summary>
    private static async Task<IResult> ResultAsync(
        ForgePlmDbContext db,
        EngineeringChangeOrder? eco,
        Guid ecoId,
        CancellationToken cancellationToken)
    {
        if (eco is null)
        {
            return NotFoundEco(ecoId);
        }

        var detail = await EcoQueries.GetDetailAsync(db, eco.Id, cancellationToken);

        return detail is null ? NotFoundEco(ecoId) : Results.Ok(detail);
    }

    private static List<NewEcoLine> ToLineCommands(IReadOnlyList<EcoLineRequest>? lines) =>
        (lines ?? [])
        .Select(line => new NewEcoLine(
            line.ChangeType,
            line.TargetComponentId,
            line.ReplacementComponentId,
            line.NewQuantity,
            line.Reason))
        .ToList();

    private static IResult NotFoundEco(Guid ecoId) => Results.Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "ECO not found.",
        detail: $"No engineering change order exists with id {ecoId}.");

    private static IResult NotFoundProduct(Guid productId) => Results.Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Product not found.",
        detail: $"No product exists with id {productId}.");
}
