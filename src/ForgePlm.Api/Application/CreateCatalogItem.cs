using System.Text.Json;
using ForgePlm.Api.Persistence;
using ForgePlm.Domain.Audit;
using ForgePlm.Domain.Components;
using ForgePlm.Domain.Products;

namespace ForgePlm.Api.Application;

public sealed record NewProduct(
    string? PartNumber,
    string? Name,
    string? Category,
    ProductLifecycleStatus LifecycleStatus,
    string? Actor);

public sealed record NewComponent(
    string? PartNumber,
    string? Name,
    string? Category,
    ComponentLifecycleStatus LifecycleStatus,
    string? Actor);

/// <summary>Create a product and its audit event in one database save.</summary>
public sealed class CreateProduct(ForgePlmDbContext db)
{
    public async Task<Product> ExecuteAsync(NewProduct command, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var product = Product.Create(
            command.PartNumber,
            command.Name,
            command.Category,
            command.LifecycleStatus,
            now);

        db.Products.Add(product);
        db.AuditEvents.Add(AuditEvent.Record(
            command.Actor ?? string.Empty,
            AuditActions.ProductCreated,
            "Product",
            product.Id,
            JsonSerializer.Serialize(new { partNumber = product.PartNumber, name = product.Name }),
            now));

        await db.SaveChangesAsync(cancellationToken);
        return product;
    }
}

/// <summary>Create a component and its audit event in one database save.</summary>
public sealed class CreateComponent(ForgePlmDbContext db)
{
    public async Task<Component> ExecuteAsync(NewComponent command, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var component = Component.Create(
            command.PartNumber,
            command.Name,
            command.Category,
            command.LifecycleStatus,
            now);

        db.Components.Add(component);
        db.AuditEvents.Add(AuditEvent.Record(
            command.Actor ?? string.Empty,
            AuditActions.ComponentCreated,
            "Component",
            component.Id,
            JsonSerializer.Serialize(new { partNumber = component.PartNumber, name = component.Name }),
            now));

        await db.SaveChangesAsync(cancellationToken);
        return component;
    }
}
