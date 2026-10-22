using ForgePlm.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ForgePlm.Api.Application;

/// <summary>
/// Generates the human-readable ECO number (`ECO-{yyyy}-{NNNN}`, A-04).
///
/// The number is server-side only and never reused: the unique index `ux_ecos_eco_number` is
/// the real guarantee, and callers retry when PostgreSQL refuses a number another request
/// took in the same instant. There is deliberately no sequence table — the sequence restarts
/// each year, so the "next" value is derived from the current year's highest number rather
/// than from a counter that would have to be reset.
/// </summary>
public static class EcoNumberGenerator
{
    public static async Task<string> NextAsync(
        ForgePlmDbContext db,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var prefix = $"ECO-{utcNow.Year:D4}-";

        // Fixed-width (4-digit) padding means every number of a year has the same length, so
        // parsing is safe and unambiguous. Materialising the year's numbers is bounded by the
        // demo's scale; a production system would use a per-year sequence.
        var numbers = await db.EngineeringChangeOrders.AsNoTracking()
            .Where(e => e.EcoNumber.StartsWith(prefix))
            .Select(e => e.EcoNumber)
            .ToListAsync(cancellationToken);

        var highest = numbers
            .Select(number => int.TryParse(number.AsSpan(prefix.Length), out var value) ? value : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"{prefix}{highest + 1:D4}";
    }
}
