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
    public const string BlankPartNumber = "blank-part-number";
    public const string PartNumberTooLong = "part-number-too-long";
    public const string BlankName = "blank-name";
    public const string NameTooLong = "name-too-long";
    public const string BlankTitle = "blank-title";
    public const string BlankActor = "blank-actor";
    public const string NonPositiveQuantity = "non-positive-quantity";
    public const string DuplicateComponentInRevision = "duplicate-component-in-revision";
    public const string InvalidRevisionCode = "invalid-revision-code";
    public const string RevisionLimitReached = "revision-limit-reached";
    public const string InvalidTransition = "invalid-transition";
    public const string StaleBaseRevision = "stale-base-revision";
    public const string EcoLinesRequired = "eco-lines-required";
    public const string RejectionReasonRequired = "rejection-reason-required";
    public const string ComponentNotActive = "component-not-active";
    public const string ProductRetired = "product-retired";
    public const string TargetNotInBaseRevision = "target-component-not-in-base-revision";
    public const string ReplacementMustDiffer = "replacement-must-differ-from-target";
}
