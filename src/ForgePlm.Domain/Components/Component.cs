using ForgePlm.Domain.Common;

namespace ForgePlm.Domain.Components;

public enum ComponentLifecycleStatus
{
    Draft = 0,
    Active = 1,
    Retired = 2,
}

/// <summary>
/// A reusable part that may appear in one or more products' BOMs (spec §5.2).
///
/// Part number, name and category are immutable in v1: there is no update endpoint.
/// That is not laziness — it is what makes reference-only history truthful. If component
/// editing is ever added, historical BOM snapshots become mandatory (spec §16, A-07).
/// </summary>
public class Component
{
    public const int MaxNameLength = 200;

    public Guid Id { get; private set; }

    /// <summary>Normalised (trimmed, upper-cased), immutable after creation, unique case-insensitively.</summary>
    public string PartNumber { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string Category { get; private set; } = string.Empty;

    public ComponentLifecycleStatus LifecycleStatus { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    private Component()
    {
        // EF Core materialisation only.
    }

    /// <param name="id">Optional fixed identifier for deterministic seeding (A-12).</param>
    public static Component Create(
        string? partNumber,
        string? name,
        string? category,
        ComponentLifecycleStatus lifecycleStatus,
        DateTime utcNow,
        Guid? id = null)
    {
        return new Component
        {
            Id = id ?? Guid.NewGuid(),
            PartNumber = Common.PartNumber.From(partNumber).Value,
            Name = EnsureNameNotBlank(name),
            Category = (category ?? string.Empty).Trim(),
            LifecycleStatus = lifecycleStatus,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
    }

    /// <summary>True when the component may be proposed or released into a BOM.</summary>
    public bool IsActive => LifecycleStatus == ComponentLifecycleStatus.Active;

    /// <summary>
    /// Rule from spec §5.2/§5.3: a retired (or otherwise non-active) component cannot be
    /// added to a newly proposed or newly released BOM. Historical revisions that already
    /// reference it are unaffected — they retain their reference (spec §5.2).
    /// </summary>
    public void EnsureCanBeUsedInNewBom()
    {
        if (!IsActive)
        {
            throw new DomainConflictException(
                DomainErrorCodes.ComponentNotActive,
                $"Component {PartNumber} is {LifecycleStatus} and cannot be used in a new BOM revision.");
        }
    }

    private static string EnsureNameNotBlank(string? name)
    {
        var normalised = (name ?? string.Empty).Trim();

        if (normalised.Length == 0)
        {
            throw new DomainRuleViolationException(
                DomainErrorCodes.BlankName,
                "Component name cannot be blank.");
        }

        if (normalised.Length > MaxNameLength)
        {
            throw new DomainRuleViolationException(
                DomainErrorCodes.NameTooLong,
                $"Component name cannot exceed {MaxNameLength} characters.");
        }

        return normalised;
    }
}
