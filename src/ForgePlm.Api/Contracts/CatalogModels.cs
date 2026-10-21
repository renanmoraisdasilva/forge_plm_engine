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
