namespace ForgePlm.Domain.Common;

/// <summary>
/// A part number, normalised once at the boundary and compared case-insensitively by
/// virtue of being stored upper-case.
///
/// The entity stores the plain string (see <c>Product.PartNumber</c>) rather than this
/// type, so EF Core needs no value conversion. Correctness is preserved because the
/// only way a part number enters the domain is through <see cref="From"/>, which
/// normalises and validates. That is the cheapest enforcement that still holds.
/// </summary>
public readonly record struct PartNumber
{
    public const int MaxLength = 50;

    public string Value { get; }

    private PartNumber(string value) => Value = value;

    public static PartNumber From(string? raw)
    {
        var normalised = (raw ?? string.Empty).Trim().ToUpperInvariant();

        if (normalised.Length == 0)
        {
            throw new DomainRuleViolationException(
                DomainErrorCodes.BlankPartNumber,
                "Part number cannot be blank.");
        }

        if (normalised.Length > MaxLength)
        {
            throw new DomainRuleViolationException(
                DomainErrorCodes.PartNumberTooLong,
                $"Part number cannot exceed {MaxLength} characters.");
        }

        return new PartNumber(normalised);
    }

    /// <summary>Returns the normalised value, or null when the input cannot become a part number.</summary>
    public static string? TryNormalize(string? raw)
    {
        var normalised = (raw ?? string.Empty).Trim().ToUpperInvariant();
        return normalised.Length == 0 || normalised.Length > MaxLength ? null : normalised;
    }

    public override string ToString() => Value ?? string.Empty;
}
