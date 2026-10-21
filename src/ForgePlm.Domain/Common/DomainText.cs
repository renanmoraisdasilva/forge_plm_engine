namespace ForgePlm.Domain.Common;

/// <summary>
/// Normalisation and length rules for free text that is stored in a bounded column.
///
/// The limits live here rather than only in the EF configuration: a value that PostgreSQL
/// would reject with `22001` must be rejected by the domain first, with a `400` the UI can
/// show as a field error. The configuration constants and these checks deliberately share
/// one number so the two cannot drift.
/// </summary>
public static class DomainText
{
    /// <summary>Trimmed optional text; over-length text is a rule violation, not a database error.</summary>
    public static string Optional(string? value, int maxLength, string fieldName)
    {
        var normalised = (value ?? string.Empty).Trim();
        EnsureMaxLength(normalised, maxLength, fieldName);
        return normalised;
    }

    /// <summary>Trimmed required text. The caller supplies the stable code for the blank case.</summary>
    public static string Required(string? value, int maxLength, string fieldName, string blankCode)
    {
        var normalised = (value ?? string.Empty).Trim();

        if (normalised.Length == 0)
        {
            throw new DomainRuleViolationException(blankCode, $"{fieldName} is required.");
        }

        EnsureMaxLength(normalised, maxLength, fieldName);
        return normalised;
    }

    private static void EnsureMaxLength(string normalised, int maxLength, string fieldName)
    {
        if (normalised.Length > maxLength)
        {
            throw new DomainRuleViolationException(
                DomainErrorCodes.TextTooLong,
                $"{fieldName} cannot exceed {maxLength} characters.");
        }
    }
}
