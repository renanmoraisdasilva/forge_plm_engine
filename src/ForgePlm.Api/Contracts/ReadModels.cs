using ForgePlm.Domain.BillOfMaterials;
using ForgePlm.Domain.Components;
using ForgePlm.Domain.Products;

namespace ForgePlm.Api.Contracts;

/// <summary>
/// Read models for products, components and BOM revisions (spec §7).
///
/// These are projections, never EF entities — spec §7 says so outright, and it matters here
/// because `BomRevision.Lines` is an aggregate-internal collection: serialising the entity
/// would hand the client a graph the domain deliberately does not expose as mutable.
///
/// Lifecycle enums go out as strings. Storing them as strings in the database keeps the
/// contract readable and stable if a value is ever renamed — a numeric enum would silently
/// change meaning.
/// </summary>
public sealed record ProductDto(
    Guid Id,
    string PartNumber,
    string Name,
    string Category,
    string LifecycleStatus,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record ComponentDto(
    Guid Id,
    string PartNumber,
    string Name,
    string Category,
    string LifecycleStatus,
    DateTime CreatedAtUtc);

/// <summary>Enough to render a BOM row or an ECO's target without a second round trip.</summary>
public sealed record ComponentSummaryDto(Guid Id, string PartNumber, string Name, string LifecycleStatus);

public sealed record BomLineDto(
    Guid Id,
    Guid ComponentId,
    int Quantity,
    string Unit,
    ComponentSummaryDto Component);

public sealed record BomRevisionDto(
    Guid Id,
    Guid ProductId,
    string RevisionCode,
    string Status,
    DateTime? EffectiveAtUtc,
    DateTime? ReleasedAtUtc,
    DateTime CreatedAtUtc,
    string CreatedBy,
    Guid? SourceEcoId,
    int LineCount);

/// <summary>
/// The current revision plus its lines.
///
/// The spec splits `/bom-revisions/current` and `/bom-revisions/{id}/lines` into two endpoints,
/// and both still exist. This response includes the lines as well, because the product detail
/// screen — the first stop on the protected demo path (D-02c) — cannot render anything useful
/// from a revision header alone, and asking the client to make a second call for the obvious
/// case is a contract shaped around the server's convenience. Additive, so nothing breaks.
/// </summary>
public sealed record CurrentRevisionResponse(BomRevisionDto Revision, IReadOnlyList<BomLineDto> Lines);

internal static class DtoMappings
{
    public static ProductDto ToDto(this Product p) => new(
        p.Id, p.PartNumber, p.Name, p.Category,
        p.LifecycleStatus.ToString(), p.CreatedAtUtc, p.UpdatedAtUtc);

    public static ComponentDto ToDto(this Component c) => new(
        c.Id, c.PartNumber, c.Name, c.Category, c.LifecycleStatus.ToString(), c.CreatedAtUtc);

    public static ComponentSummaryDto ToSummary(this Component c) => new(
        c.Id, c.PartNumber, c.Name, c.LifecycleStatus.ToString());

    public static BomLineDto ToDto(this BomLine line, Component component) => new(
        line.Id, line.ComponentId, line.Quantity, line.Unit, component.ToSummary());

    public static BomRevisionDto ToDto(this BomRevision revision, int lineCount) => new(
        revision.Id,
        revision.ProductId,
        revision.RevisionCode,
        revision.Status.ToString(),
        revision.EffectiveAtUtc,
        revision.ReleasedAtUtc,
        revision.CreatedAtUtc,
        revision.CreatedBy,
        revision.SourceEcoId,
        lineCount);
}
