namespace ForgePlm.Domain.Common;

/// <summary>
/// Base type for rules enforced by the domain itself.
///
/// The two subclasses exist because they map to different HTTP outcomes, and that
/// mapping must never be a string match on a message:
///   - <see cref="DomainRuleViolationException"/>  -> 400 (the request asked for something invalid)
///   - <see cref="DomainConflictException"/>       -> 409 (valid request, but the world has moved on)
/// </summary>
public abstract class DomainException : Exception
{
    /// <summary>Stable machine-readable code, surfaced to clients as `code` / `conflictType`.</summary>
    public string Code { get; }

    protected DomainException(string code, string message) : base(message)
    {
        Code = code;
    }
}

/// <summary>The request itself is invalid: blank name, non-positive quantity, missing reason.</summary>
public sealed class DomainRuleViolationException : DomainException
{
    public DomainRuleViolationException(string code, string message) : base(code, message)
    {
    }
}

/// <summary>The request was well-formed, but an invariant about current state forbids it:
/// invalid status transition, stale base revision, an already-released revision.</summary>
public sealed class DomainConflictException : DomainException
{
    public DomainConflictException(string code, string message) : base(code, message)
    {
    }
}

/// <summary>
/// Every code the domain can emit. Clients (and the UI) key off these, not off prose,
/// so adding one here is an API contract change.
/// </summary>
public static class DomainErrorCodes
{
    // --- Field validation (400) -------------------------------------------------
    public const string BlankPartNumber = "blank-part-number";
    public const string PartNumberTooLong = "part-number-too-long";
    public const string BlankName = "blank-name";
    public const string NameTooLong = "name-too-long";
    public const string BlankTitle = "blank-title";
    public const string BlankActor = "blank-actor";
    public const string BlankEcoNumber = "blank-eco-number";
    public const string NonPositiveQuantity = "non-positive-quantity";

    // --- Missing required references (400) --------------------------------------
    // These used to share `blank-part-number`, which made the code a lie: a missing base
    // revision is not a blank part number, and the UI keys off these strings.
    public const string BaseRevisionRequired = "base-revision-required";
    public const string TargetComponentRequired = "target-component-required";
    public const string ReplacementComponentRequired = "replacement-component-required";
    public const string ComponentRequired = "component-required";
    public const string ApprovalRevisionRequired = "approval-revision-required";
    public const string EcoLinesRequired = "eco-lines-required";
    public const string BomLinesRequired = "bom-lines-required";
    public const string DuplicateTargetComponent = "duplicate-target-component";
    public const string InvalidFilterValue = "invalid-filter-value";

    // --- State conflicts (409) --------------------------------------------------
    public const string InvalidRevisionCode = "invalid-revision-code";
    public const string RevisionLimitReached = "revision-limit-reached";
    public const string InvalidTransition = "invalid-transition";
    public const string StaleBaseRevision = "stale-base-revision";
    public const string DuplicateComponentInRevision = "duplicate-component-in-revision";
    public const string TargetNotInBaseRevision = "target-component-not-in-base-revision";
    public const string ReplacementMustDiffer = "replacement-must-differ-from-target";
    public const string ComponentNotActive = "component-not-active";
    public const string ProductRetired = "product-retired";
    public const string RejectionReasonRequired = "rejection-reason-required";

    // --- Database-enforced conflicts (409) --------------------------------------
    // Never thrown by the domain. Emitted by the API when a PostgreSQL 23505 maps to a
    // conflict. Declared here so the mapping has one home and the contract stays closed.
    // The values match the index names they correspond to — see ErrorResponseMapper.
    public const string DuplicatePartNumber = "duplicate-part-number";
    public const string DuplicateEcoNumber = "duplicate-eco-number";
    public const string DuplicateRevisionCode = "duplicate-revision-code";
    public const string DuplicateComponentInRevisionLine = "duplicate-component-in-revision-line";
    public const string ConflictingReleasedRevision = "conflicting-released-revision";
    public const string ConcurrencyConflict = "concurrency-conflict";
}
