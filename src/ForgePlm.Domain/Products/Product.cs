using ForgePlm.Domain.Common;

namespace ForgePlm.Domain.Products;

public enum ProductLifecycleStatus
{
    Draft = 0,
    Active = 1,
    Retired = 2,
}

/// <summary>
/// A sellable or buildable top-level item whose configuration is controlled (spec §5.1).
///
/// The product deliberately does NOT own its BOM revisions as a child collection:
/// revision history is unbounded and is queried separately. What the product does own
/// is the single rule that depends on it — a retired product cannot receive a newly
/// released revision.
/// </summary>
public class Product
{
    public const int MaxNameLength = 200;
    public const int MaxCategoryLength = 200;

    public Guid Id { get; private set; }

    /// <summary>Normalised (trimmed, upper-cased), immutable after creation, unique case-insensitively.</summary>
    public string PartNumber { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string Category { get; private set; } = string.Empty;

    public ProductLifecycleStatus LifecycleStatus { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    private Product()
    {
        // EF Core materialisation only.
    }

    /// <param name="id">
    /// Optional fixed identifier. The seed supplies one so re-application is a no-op and
    /// demo URLs stay stable (A-12); normal creation leaves it null and takes a new Guid.
    /// </param>
    public static Product Create(
        string? partNumber,
        string? name,
        string? category,
        ProductLifecycleStatus lifecycleStatus,
        DateTime utcNow,
        Guid? id = null)
    {
        var product = new Product
        {
            Id = id ?? Guid.NewGuid(),
            PartNumber = Common.PartNumber.From(partNumber).Value,
            Name = EnsureNameNotBlank(name),
            Category = DomainText.Optional(category, MaxCategoryLength, "Product category"),
            LifecycleStatus = lifecycleStatus,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };

        return product;
    }

    /// <summary>
    /// Rule from spec §5.1: a retired product cannot receive a newly released BOM revision.
    /// Called by the release use case, before any revision is mutated.
    /// </summary>
    public void EnsureCanReceiveNewlyReleasedRevision()
    {
        if (LifecycleStatus == ProductLifecycleStatus.Retired)
        {
            throw new DomainConflictException(
                DomainErrorCodes.ProductRetired,
                $"Product {PartNumber} is retired and cannot receive a newly released BOM revision.");
        }
    }

    /// <summary>
    /// Rule from spec §8: an ECO may only be created against an active product.
    /// </summary>
    public void EnsureIsActiveForChangeProposal()
    {
        if (LifecycleStatus != ProductLifecycleStatus.Active)
        {
            throw new DomainConflictException(
                DomainErrorCodes.InvalidTransition,
                $"Product {PartNumber} is {LifecycleStatus}; only active products can receive an ECO.");
        }
    }

    /// <summary>
    /// Records that the product's controlled configuration changed (approval, release).
    ///
    /// This does two jobs deliberately:
    ///   * it maintains <see cref="UpdatedAtUtc"/>, which was creation-only until now;
    ///   * it forces an UPDATE of the product row, which is what makes the `xmin`
    ///     concurrency token observable to the approve/release transactions (ADR-004).
    ///     Approve and release do not otherwise modify the product, so without a write the
    ///     token would never be checked and two competing releases could both see "no change".
    ///
    /// This method changes no lifecycle state and applies no rule. The rules for *who may*
    /// cause a configuration change live on the ECO and revision aggregates.
    /// </summary>
    public void MarkConfigurationChanged(DateTime utcNow) => UpdatedAtUtc = utcNow;

    private static string EnsureNameNotBlank(string? name)
    {
        var normalised = (name ?? string.Empty).Trim();

        if (normalised.Length == 0)
        {
            throw new DomainRuleViolationException(
                DomainErrorCodes.BlankName,
                "Product name cannot be blank.");
        }

        if (normalised.Length > MaxNameLength)
        {
            throw new DomainRuleViolationException(
                DomainErrorCodes.NameTooLong,
                $"Product name cannot exceed {MaxNameLength} characters.");
        }

        return normalised;
    }
}
