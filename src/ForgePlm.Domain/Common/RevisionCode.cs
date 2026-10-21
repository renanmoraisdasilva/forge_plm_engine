namespace ForgePlm.Domain.Common;

/// <summary>
/// Revision codes: `A`, `B`, `C`, … `Z`, assigned per product in sequence.
///
/// Deliberately no rollover past `Z` (spec §5.3). A product with more than 26 revisions
/// is out of scope for this prototype, and inventing `AA` silently would be a domain
/// decision made by accident. Failing loudly is the honest behaviour.
/// </summary>
public static class RevisionCode
{
    public const int MaxCode = 'Z';

    public static string EnsureValid(string? code)
    {
        var normalised = (code ?? string.Empty).Trim().ToUpperInvariant();

        if (normalised.Length != 1 || normalised[0] < 'A' || normalised[0] > MaxCode)
        {
            throw new DomainRuleViolationException(
                DomainErrorCodes.InvalidRevisionCode,
                $"Revision code must be a single letter A-Z (received '{code}').");
        }

        return normalised;
    }

    /// <summary>The successor of a revision code, e.g. `A` -> `B`. Throws rather than rolling over.</summary>
    public static string Next(string? current)
    {
        var normalised = EnsureValid(current);

        if (normalised[0] == MaxCode)
        {
            // Spec §7 lists `revision-limit-reached` among the `409` conflict types, not the
            // `400` validation codes: the request is well-formed, the product's revision
            // history is simply exhausted. Throwing a rule violation here made the API map a
            // conflict to 400.
            throw new DomainConflictException(
                DomainErrorCodes.RevisionLimitReached,
                "Revision codes stop at Z; this prototype does not roll over to AA.");
        }

        return ((char)(normalised[0] + 1)).ToString();
    }
}
