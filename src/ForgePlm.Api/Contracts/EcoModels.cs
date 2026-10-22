using System.Text.Json;
using ForgePlm.Domain.EngineeringChanges;

namespace ForgePlm.Api.Contracts;

/// <summary>
/// Request and response shapes for engineering changes (spec §7,
/// `docs/IMPLEMENTATION.md`).
///
/// Requests carry only what a client may decide. `ecoNumber`, `baseReleasedRevisionId` and
/// the creator are absent on purpose: the first two are resolved by the server, and the
/// creator was decided when the draft was created. Draft edit requests carry the current actor
/// separately so audit events identify the editor.
/// </summary>
public sealed record EcoLineRequest(
    EngineeringChangeType ChangeType,
    Guid TargetComponentId,
    Guid? ReplacementComponentId,
    int? NewQuantity,
    string? Reason);

public sealed record CreateEcoRequest(
    Guid ProductId,
    string? Title,
    string? ReasonForChange,
    string? ImpactAssessment,
    DateTime? RequestedEffectiveAtUtc,
    string? Actor,
    IReadOnlyList<EcoLineRequest>? Lines);

/// <summary>Draft edit (spec §7). The product is fixed; actor identifies the person editing.</summary>
public sealed record UpdateEcoRequest(
    string? Title,
    string? ReasonForChange,
    string? ImpactAssessment,
    DateTime? RequestedEffectiveAtUtc,
    string? Actor,
    IReadOnlyList<EcoLineRequest>? Lines);

public sealed record ActorRequest(string? Actor);

public sealed record ApproveEcoRequest(string? Actor, string? Comment);

public sealed record RejectEcoRequest(string? Actor, string? RejectionReason);

/// <summary>
/// Future requested effectivity must be explicitly acknowledged before immediate release.
/// </summary>
public sealed record ReleaseEcoRequest(string? Actor, bool ConfirmImmediateEffectivity = false);

/// <summary>A component reference, denormalised into ECO lines so a line renders without a lookup.</summary>
public sealed record ComponentRefDto(Guid Id, string PartNumber, string Name);

public sealed record EcoProductDto(Guid Id, string PartNumber, string Name);

public sealed record EcoLineDto(
    Guid Id,
    string ChangeType,
    Guid TargetComponentId,
    ComponentRefDto Target,
    Guid? ReplacementComponentId,
    ComponentRefDto? Replacement,
    int? NewQuantity,
    string? Reason);

public sealed record EcoSummaryDto(
    Guid Id,
    string EcoNumber,
    string Title,
    Guid ProductId,
    EcoProductDto Product,
    string Status,
    string CreatedBy,
    DateTime CreatedAtUtc,
    int LineCount);

public sealed record EcoDetailDto(
    Guid Id,
    string EcoNumber,
    string Title,
    Guid ProductId,
    EcoProductDto Product,
    string Status,
    string CreatedBy,
    DateTime CreatedAtUtc,
    int LineCount,
    Guid BaseReleasedRevisionId,
    string BaseRevisionCode,
    string ReasonForChange,
    string ImpactAssessment,
    DateTime? RequestedEffectiveAtUtc,
    DateTime? SubmittedAtUtc,
    DateTime? DecidedAtUtc,
    string? DecidedBy,
    string? ReviewerComment,
    Guid? ApprovedRevisionId,
    IReadOnlyList<EcoLineDto> Lines);

/// <summary>
/// One audit row. `Details` is a JSON object in the database and is surfaced as an object,
/// not a string, so the timeline can render it without a second parse on the client.
/// </summary>
public sealed record AuditEventDto(
    Guid Id,
    DateTime OccurredAtUtc,
    string Actor,
    string Action,
    string EntityType,
    Guid EntityId,
    JsonElement Details);
