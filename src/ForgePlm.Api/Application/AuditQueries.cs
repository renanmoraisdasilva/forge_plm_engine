using System.Text.Json;
using ForgePlm.Api.Contracts;
using ForgePlm.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ForgePlm.Api.Application;

/// <summary>
/// The audit read (spec §7), newest first. Read-only by design: nothing may edit or delete an
/// audit row (spec §5.5).
/// </summary>
public static class AuditQueries
{
    /// <summary>The entity types the audit screen can filter on.</summary>
    public static readonly IReadOnlyList<string> EntityTypes =
        ["Product", "Component", "BomRevision", "EngineeringChangeOrder"];

    public static async Task<PagedResponse<AuditEventDto>> ListAsync(
        ForgePlmDbContext db,
        string? entityType,
        Guid? entityId,
        PageRequest paging,
        CancellationToken cancellationToken = default)
    {
        var query = db.AuditEvents.AsNoTracking();

        if (entityType is not null)
        {
            query = query.Where(audit => audit.EntityType == entityType);
        }

        if (entityId is not null)
        {
            query = query.Where(audit => audit.EntityId == entityId);
        }

        var total = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderByDescending(audit => audit.OccurredAtUtc)
            .ThenByDescending(audit => audit.Id)
            .Skip(paging.Skip)
            .Take(paging.EffectivePageSize)
            .Select(audit => new
            {
                audit.Id,
                audit.OccurredAtUtc,
                audit.Actor,
                audit.Action,
                audit.EntityType,
                audit.EntityId,
                audit.Details,
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(row => new AuditEventDto(
                row.Id,
                row.OccurredAtUtc,
                row.Actor,
                row.Action,
                row.EntityType,
                row.EntityId,
                ParseDetails(row.Details)))
            .ToList();

        return new PagedResponse<AuditEventDto>(items, total, paging.EffectivePage, paging.EffectivePageSize);
    }

    /// <summary>
    /// The column stores JSON text, and the contract exposes `details` as an object. A malformed
    /// payload is surfaced as `{}` rather than breaking the whole timeline: the audit row is
    /// still the evidence, and a read endpoint must not fail because one detail blob is corrupt.
    /// </summary>
    private static JsonElement ParseDetails(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return EmptyObject();
        }

        try
        {
            using var document = JsonDocument.Parse(json);

            // Clone detaches the element from the document, which is disposed on leaving scope.
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return EmptyObject();
        }
    }

    private static JsonElement EmptyObject()
    {
        using var document = JsonDocument.Parse("{}");
        return document.RootElement.Clone();
    }
}
