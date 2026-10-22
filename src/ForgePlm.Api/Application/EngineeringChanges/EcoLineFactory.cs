using ForgePlm.Domain.EngineeringChanges;

namespace ForgePlm.Api.Application;

/// <summary>
/// Turns the request lines carried by a create or edit command into domain lines. Shared by both
/// so a draft and an edit cannot disagree about what a line means.
/// </summary>
internal static class EcoLineFactory
{
    /// <summary>
    /// A missing replacement or quantity is passed through as an empty/zero value so the domain —
    /// which owns those rules and their codes — rejects it, rather than the API inventing a second
    /// validation vocabulary.
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
