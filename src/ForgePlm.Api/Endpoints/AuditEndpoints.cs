using ForgePlm.Api.Application;
using ForgePlm.Api.Contracts;
using ForgePlm.Api.Persistence;
using ForgePlm.Domain.Common;

namespace ForgePlm.Api.Endpoints;

/// <summary>Audit read (spec §7, §5.5). Paged, newest first, filterable by entity.</summary>
public static class AuditEndpoints
{
    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/audit", ListAsync)
            .WithName("ListAuditEvents")
            .WithSummary("Paged audit trail, newest first, optionally filtered by entity type and id.");

        return app;
    }

    private static async Task<IResult> ListAsync(
        ForgePlmDbContext db,
        string? entityType,
        Guid? entityId,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken)
    {
        var paging = PageRequest.From(page, pageSize);

        var result = await AuditQueries.ListAsync(
            db,
            CanonicalEntityType(entityType),
            entityId,
            paging,
            cancellationToken);

        return Results.Ok(result);
    }

    /// <summary>
    /// A filter the audit endpoint cannot express is a malformed request, not an empty result —
    /// same rule as the status filters on the list endpoints.
    /// </summary>
    private static string? CanonicalEntityType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        var canonical = AuditQueries.EntityTypes
            .FirstOrDefault(candidate => string.Equals(candidate, trimmed, StringComparison.OrdinalIgnoreCase));

        return canonical ?? throw new DomainRuleViolationException(
            DomainErrorCodes.InvalidFilterValue,
            $"'{value}' is not a valid entityType. Expected one of: {string.Join(", ", AuditQueries.EntityTypes)}.");
    }
}
