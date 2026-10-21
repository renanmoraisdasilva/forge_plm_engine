using ForgePlm.Domain.Components;
using ForgePlm.Domain.Products;

namespace ForgePlm.Api.Contracts;

/// <summary>Request bodies for API-only product and component creation.</summary>
public sealed record CreateProductRequest(
    string? PartNumber,
    string? Name,
    string? Category,
    ProductLifecycleStatus LifecycleStatus,
    string? Actor);

public sealed record CreateComponentRequest(
    string? PartNumber,
    string? Name,
    string? Category,
    ComponentLifecycleStatus LifecycleStatus,
    string? Actor);

/// <summary>Initial released BOM for a product that has no revision history yet.</summary>
public sealed record CreateInitialBomRequest(
    IReadOnlyList<InitialBomLineRequest>? Lines,
    string? Actor);

public sealed record InitialBomLineRequest(Guid ComponentId, int Quantity, string? Unit);
