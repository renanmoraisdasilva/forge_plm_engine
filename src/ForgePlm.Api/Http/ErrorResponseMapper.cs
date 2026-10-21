using ForgePlm.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ForgePlm.Api.Http;

/// <summary>
/// The one place failures become RFC 7807 responses (spec §7, decisions A-10 and A-21).
///
/// Written before any endpoint exists on purpose. The mapping is the API contract's most
/// cross-cutting part; inventing it per-endpoint is how a codebase ends up returning 400 for
/// something the UI has been told to treat as a 409, which is exactly the lying contract the
/// 2026-10-21 review caught in the domain error codes.
///
/// Endpoints do not call this. They let exceptions propagate and the registered
/// <see cref="ForgePlmExceptionHandler"/> picks them up, which is what keeps them thin
/// (spec §14) and makes the mapping impossible to forget on a new endpoint.
/// </summary>
public static class ErrorResponseMapper
{
    /// <summary>Generic fallback so a constraint added later still produces a sane 409.</summary>
    private const string UnknownUniqueViolation = "duplicate-value";

    /// <summary>
    /// PostgreSQL reports a unique violation by index name, not by meaning. Translating here —
    /// rather than matching on the error message — keeps the mapping stable across locale and
    /// across minor PostgreSQL releases.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> UniqueIndexConflictTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ux_products_part_number"] = DomainErrorCodes.DuplicatePartNumber,
            ["ux_components_part_number"] = DomainErrorCodes.DuplicatePartNumber,
            ["ux_ecos_eco_number"] = DomainErrorCodes.DuplicateEcoNumber,
            ["ux_bom_revisions_product_revision_code"] = DomainErrorCodes.DuplicateRevisionCode,
            ["ux_bom_lines_revision_component"] = DomainErrorCodes.DuplicateComponentInRevisionLine,

            // ADR-004's partial unique index. Reaching this means two releases raced; the
            // intended user-visible outcome is a stale-base conflict, not a crash.
            ["ux_bom_revisions_one_released_per_product"] = DomainErrorCodes.ConflictingReleasedRevision,
        };

    /// <summary>
    /// Returns `null` for exceptions this layer does not own, so they stay 500s. Disguising a
    /// server fault as a 400 would hide a real bug behind a message the UI shows as user error.
    /// </summary>
    public static ProblemDetails? For(Exception exception) => exception switch
    {
        DomainRuleViolationException rule => Problem(
            StatusCodes.Status400BadRequest, "A request was invalid.", rule.Message, rule.Code),

        // Spec §7: every 409 carries a `conflictType` so the UI can react without matching on
        // prose. `Code` is that value — one vocabulary, not two.
        DomainConflictException conflict => Problem(
            StatusCodes.Status409Conflict, "The request conflicts with the current state.",
            conflict.Message, conflict.Code, conflict.Code),

        // Two ECOs raced. The loser is told the world moved on, not shown a stack trace.
        DbUpdateConcurrencyException => Problem(
            StatusCodes.Status409Conflict,
            "The record changed while this request was in flight.",
            "Reload the current state and retry.",
            DomainErrorCodes.ConcurrencyConflict, DomainErrorCodes.ConcurrencyConflict),

        DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } unique } =>
            Problem(
                StatusCodes.Status409Conflict, "A unique constraint was violated.", unique.Message,
                ConflictTypeFor(unique), ConflictTypeFor(unique)),

        _ => null,
    };

    private static string ConflictTypeFor(PostgresException violation) =>
        violation.ConstraintName is { } name && UniqueIndexConflictTypes.TryGetValue(name, out var type)
            ? type
            : UnknownUniqueViolation;

    private static ProblemDetails Problem(
        int statusCode,
        string title,
        string detail,
        string? code,
        string? conflictType = null)
    {
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,

            // RFC 7807's `type` should be a URI reference. Reusing the stable code keeps it
            // machine-readable without minting a documentation URL that would 404.
            Type = code,
        };

        if (code is not null)
        {
            problem.Extensions["code"] = code;
        }

        if (conflictType is not null)
        {
            problem.Extensions["conflictType"] = conflictType;
        }

        return problem;
    }
}

/// <summary>
/// Hands every unhandled exception to <see cref="ErrorResponseMapper"/>. Returning `false` for
/// anything the mapper does not own defers to the framework's 500 handling, so a genuine bug
/// still surfaces as one.
/// </summary>
public sealed class ForgePlmExceptionHandler(ILogger<ForgePlmExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var problem = ErrorResponseMapper.For(exception);

        if (problem is null)
        {
            // Not a domain or persistence conflict. Log it and let it become a 500.
            logger.LogError(exception, "Unhandled exception for {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);
            return false;
        }

        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);

        return true;
    }
}
