namespace ForgePlm.Api.Application;

/// <summary>
/// Normalises timestamps to UTC at the boundary. `System.Text.Json` gives `Utc` for a `Z`
/// literal and `Local` for an offset; an unspecified value is trusted as UTC because the
/// contract says ISO-8601 UTC. Storing a local-time value would make A-15's "UTC stored,
/// local displayed" impossible to honour later.
/// </summary>
internal static class UtcTimestamp
{
    public static DateTime Normalise(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };

    public static DateTime? Normalise(DateTime? value) => value is null ? null : Normalise(value.Value);
}
