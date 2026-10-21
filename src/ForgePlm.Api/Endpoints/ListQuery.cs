using ForgePlm.Domain.Common;

namespace ForgePlm.Api.Endpoints;

/// <summary>
/// Query helpers shared by the list endpoints (spec §7).
/// </summary>
internal static class ListQuery
{
    /// <summary>
    /// A filter the client cannot express in the enum is a malformed request, not an empty
    /// result. Returning zero rows for `?status=Retierd` would look in the UI like "no products
    /// have that status", which is a different fact from "you misspelled it".
    /// </summary>
    public static T? ParseEnum<T>(string? value, string parameterName) where T : struct, Enum
    {
        var raw = value?.Trim();
        if (string.IsNullOrEmpty(raw))
        {
            return null;
        }

        if (Enum.TryParse<T>(raw, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
        {
            return parsed;
        }

        throw new DomainRuleViolationException(
            DomainErrorCodes.InvalidFilterValue,
            $"'{value}' is not a valid {parameterName}. Expected one of: {string.Join(", ", Enum.GetNames<T>())}.");
    }

    /// <summary>Trimmed search term, or `null` when the caller asked for no filtering.</summary>
    public static string? NormalisedTerm(string? search) =>
        string.IsNullOrEmpty(search?.Trim()) ? null : search.Trim();
}
