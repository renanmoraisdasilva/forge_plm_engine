using System.Text.Json;
using ForgePlm.Api.Persistence;
using ForgePlm.Domain.Audit;
using ForgePlm.Domain.BillOfMaterials;
using ForgePlm.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace ForgePlm.Api.Application;

public sealed record NewInitialBomLine(Guid ComponentId, int Quantity, string? Unit);

public sealed record NewInitialBom(IReadOnlyList<NewInitialBomLine>? Lines, string? Actor);

/// <summary>
/// Establish the first released configuration for a newly created product. Subsequent BOM
/// revisions must go through ECO approval and release.
/// </summary>
public sealed class CreateInitialBom(ForgePlmDbContext db)
{
    public async Task<BomRevision?> ExecuteAsync(
        Guid productId,
        NewInitialBom request,
        CancellationToken cancellationToken = default)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);
        if (product is null)
        {
            return null;
        }

        product.EnsureIsActiveForChangeProposal();

        if (await db.BomRevisions.AnyAsync(r => r.ProductId == productId, cancellationToken))
        {
            throw new DomainConflictException(
                DomainErrorCodes.InvalidTransition,
                $"Product {product.PartNumber} already has BOM revision history; create changes through an ECO.");
        }

        if (request.Lines is null || request.Lines.Count == 0)
        {
            throw new DomainRuleViolationException(DomainErrorCodes.BomLinesRequired, "An initial BOM must contain at least one line.");
        }

        var componentIds = request.Lines.Select(line => line.ComponentId).Distinct().ToList();
        var components = await db.Components.AsNoTracking()
            .Where(component => componentIds.Contains(component.Id))
            .ToDictionaryAsync(component => component.Id, cancellationToken);

        foreach (var componentId in componentIds)
        {
            if (!components.TryGetValue(componentId, out var component))
            {
                throw new DomainRuleViolationException(
                    DomainErrorCodes.ComponentRequired,
                    $"Component {componentId} does not exist.");
            }

            component.EnsureCanBeUsedInNewBom();
        }

        var now = DateTime.UtcNow;
        var lines = request.Lines
            .Select(line => BomLine.Create(line.ComponentId, line.Quantity, line.Unit))
            .ToList();
        var revision = BomRevision.CreateReleasedBaseline(
            productId,
            "A",
            lines,
            request.Actor ?? string.Empty,
            now,
            effectiveAtUtc: now);

        db.BomRevisions.Add(revision);
        db.AuditEvents.Add(AuditEvent.Record(
            request.Actor ?? string.Empty,
            AuditActions.InitialRevisionReleased,
            "BomRevision",
            revision.Id,
            JsonSerializer.Serialize(new
            {
                productId,
                revisionCode = revision.RevisionCode,
                lineCount = revision.Lines.Count,
                releasedAtUtc = now,
                effectiveAtUtc = now,
            }),
            now));

        await db.SaveChangesAsync(cancellationToken);
        return revision;
    }
}
