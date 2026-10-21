using ForgePlm.Domain.EngineeringChanges;

namespace ForgePlm.Api.Application;

/// <summary>
/// One proposed change line as the API receives it. Kept separate from the API contract so the
/// use cases can be driven directly by integration tests without constructing HTTP payloads.
/// </summary>
public sealed record NewEcoLine(
    EngineeringChangeType ChangeType,
    Guid TargetComponentId,
    Guid? ReplacementComponentId,
    int? NewQuantity,
    string? Reason);

/// <summary>Everything needed to open a draft. Base revision and ECO number are resolved server-side.</summary>
public sealed record NewEco(
    Guid ProductId,
    string? Title,
    string? ReasonForChange,
    string? ImpactAssessment,
    DateTime? RequestedEffectiveAtUtc,
    string? Actor,
    IReadOnlyList<NewEcoLine>? Lines);

/// <summary>A draft edit. Product and creator are deliberately absent (they are not editable).</summary>
public sealed record EcoEdit(
    string? Title,
    string? ReasonForChange,
    string? ImpactAssessment,
    DateTime? RequestedEffectiveAtUtc,
    IReadOnlyList<NewEcoLine>? Lines);

internal static class EcoLineFactory
{
    /// <summary>
    /// Converts request lines into domain lines. A missing replacement or quantity is passed
    /// through as an empty/zero value so the domain — which owns those rules and their codes —
    /// rejects it, rather than the API inventing a second validation vocabulary.
    /// </summary>
    public static List<EngineeringChangeLine> Build(IReadOnlyList<NewEcoLine>? lines) =>
        (lines ?? [])
        .Select(line => line.ChangeType switch
        {
            EngineeringChangeType.ReplaceComponent =>
                EngineeringChangeLine.Replace(
                    line.TargetComponentId,
                    line.ReplacementComponentId ?? Guid.Empty,
                    line.Reason),

            EngineeringChangeType.ChangeQuantity =>
                EngineeringChangeLine.ChangeQuantity(
                    line.TargetComponentId,
                    line.NewQuantity ?? 0,
                    line.Reason),

            // Unreachable: the request binds `ChangeType` through a string enum converter that
            // rejects unknown members before the handler runs. An exception here is a bug.
            _ => throw new InvalidOperationException($"Unsupported change type {line.ChangeType}."),
        })
        .ToList();
}

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
