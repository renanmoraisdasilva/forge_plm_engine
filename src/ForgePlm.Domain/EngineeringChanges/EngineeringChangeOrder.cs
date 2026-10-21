using ForgePlm.Domain.BillOfMaterials;
using ForgePlm.Domain.Common;

namespace ForgePlm.Domain.EngineeringChanges;

public enum EngineeringChangeStatus
{
    Draft = 0,
    Submitted = 1,
    Approved = 2,
    Rejected = 3,
    Applied = 4,
}

public enum EngineeringChangeType
{
    ReplaceComponent = 0,
    ChangeQuantity = 1,
}

/// <summary>
/// A controlled proposal to modify one product's BOM (spec §5.4).
///
/// The ECO records the released revision it was written against (`BaseReleasedRevisionId`)
/// and never silently rebases. If a competing ECO releases a newer revision in the meantime,
/// approval fails with a stale-base conflict and the user writes a new ECO — the demo's
/// clearest single demonstration that history is not overwritten.
/// </summary>
public class EngineeringChangeOrder
{
    public const int MaxTitleLength = 200;
    public const int MaxTextLength = 4000;

    public Guid Id { get; private set; }

    /// <summary>`ECO-2026-0042` — generated at creation, unique, never reused.</summary>
    public string EcoNumber { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public Guid ProductId { get; private set; }

    public Guid BaseReleasedRevisionId { get; private set; }

    public string ReasonForChange { get; private set; } = string.Empty;

    public string ImpactAssessment { get; private set; } = string.Empty;

    /// <summary>
    /// What the author intends, carried through to the release form. Under D-04b it is a
    /// label: releasing still takes effect immediately.
    /// </summary>
    public DateTime? RequestedEffectiveAtUtc { get; private set; }

    public EngineeringChangeStatus Status { get; private set; }

    public string CreatedBy { get; private set; } = string.Empty;

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? SubmittedAtUtc { get; private set; }

    public DateTime? DecidedAtUtc { get; private set; }

    public string? DecidedBy { get; private set; }

    public string? ReviewerComment { get; private set; }

    /// <summary>Set at approval, in the same transaction that creates the revision.</summary>
    public Guid? ApprovedRevisionId { get; private set; }

    private readonly List<EngineeringChangeLine> _lines = [];

    /// <summary>
    /// Read-only — see <see cref="BomRevision.Lines"/> for why the wrapper is not optional.
    /// </summary>
    public IReadOnlyList<EngineeringChangeLine> Lines => _lines.AsReadOnly();

    private EngineeringChangeOrder()
    {
        // EF Core materialisation only.
    }

    /// <param name="id">Optional fixed identifier for deterministic seeding (A-12).</param>
    public static EngineeringChangeOrder Create(
        string? ecoNumber,
        Guid productId,
        Guid baseReleasedRevisionId,
        string? title,
        string? reasonForChange,
        string? impactAssessment,
        DateTime? requestedEffectiveAtUtc,
        string createdBy,
        DateTime utcNow,
        Guid? id = null)
    {
        if (string.IsNullOrWhiteSpace(ecoNumber))
        {
            throw new DomainRuleViolationException(DomainErrorCodes.BlankEcoNumber, "ECO number is required.");
        }

        if (baseReleasedRevisionId == Guid.Empty)
        {
            throw new DomainRuleViolationException(
                DomainErrorCodes.BaseRevisionRequired,
                "An ECO must be created against a released base revision.");
        }

        var normalisedTitle = EnsureTitle(title);

        return new EngineeringChangeOrder
        {
            Id = id ?? Guid.NewGuid(),
            EcoNumber = ecoNumber.Trim(),
            Title = normalisedTitle,
            ProductId = productId,
            BaseReleasedRevisionId = baseReleasedRevisionId,
            ReasonForChange = (reasonForChange ?? string.Empty).Trim(),
            ImpactAssessment = (impactAssessment ?? string.Empty).Trim(),
            RequestedEffectiveAtUtc = requestedEffectiveAtUtc,
            Status = EngineeringChangeStatus.Draft,
            CreatedBy = EnsureNotBlankActor(createdBy),
            CreatedAtUtc = utcNow,
        };
    }

    /// <summary>
    /// Edits the parts of a draft an author may still change (spec §5.4: "Draft ECOs can be
    /// edited; submitted/approved/rejected/applied ECOs cannot"). Product, base revision and
    /// creator are deliberately absent: rebasing or re-attributing an ECO are not edits.
    /// </summary>
    public void UpdateDraft(
        string? title,
        string? reasonForChange,
        string? impactAssessment,
        DateTime? requestedEffectiveAtUtc,
        IEnumerable<EngineeringChangeLine> lines)
    {
        EnsureDraft();

        // Validate and replace the lines before assigning any field: a rejected line set must
        // leave the draft exactly as it was, not half-edited. ReplaceLines only mutates after
        // every rule has passed.
        var normalisedTitle = EnsureTitle(title);
        ReplaceLines(lines);

        Title = normalisedTitle;
        ReasonForChange = (reasonForChange ?? string.Empty).Trim();
        ImpactAssessment = (impactAssessment ?? string.Empty).Trim();
        RequestedEffectiveAtUtc = requestedEffectiveAtUtc;
    }

    /// <summary>
    /// Replaces the draft's lines wholesale. Draft only — submitted, approved, rejected and
    /// applied ECOs are immutable in this prototype (spec §5.4).
    /// </summary>
    public void ReplaceLines(IEnumerable<EngineeringChangeLine> lines)
    {
        EnsureDraft();

        var replacement = lines.ToList();
        if (replacement.Count == 0)
        {
            throw new DomainRuleViolationException(DomainErrorCodes.EcoLinesRequired, "An ECO must contain at least one change line.");
        }

        // Two lines targeting the same component are rejected rather than merged. Approval
        // resolves each base line with `FirstOrDefault`, so a second change to the same
        // target would be silently discarded — the approved revision would then not contain
        // every change the reviewer actually approved. Rejecting at draft is the cheap place:
        // the user still has the form open.
        var seenTargets = new HashSet<Guid>();
        foreach (var line in replacement)
        {
            if (!seenTargets.Add(line.TargetComponentId))
            {
                throw new DomainRuleViolationException(
                    DomainErrorCodes.DuplicateTargetComponent,
                    "An ECO can change a given component only once. Split it into two ECOs if two separate changes are needed.");
            }
        }

        _lines.Clear();
        _lines.AddRange(replacement);
    }

    /// <summary>
    /// Draft -> Submitted. Also validates every target line against the base revision it was
    /// written against, so a stale proposal is caught at submission, not at approval.
    /// </summary>
    public void Submit(BomRevision baseRevision, DateTime utcNow)
    {
        EnsureDraft();

        if (_lines.Count == 0)
        {
            throw new DomainRuleViolationException(DomainErrorCodes.EcoLinesRequired, "An ECO must contain at least one change line before submission.");
        }

        if (baseRevision.Id != BaseReleasedRevisionId)
        {
            throw new DomainConflictException(
                DomainErrorCodes.StaleBaseRevision,
                $"ECO {EcoNumber} was created against revision {baseRevision.RevisionCode}, which is no longer the base revision.");
        }

        foreach (var line in _lines)
        {
            line.EnsureTargetExistsIn(baseRevision);
        }

        Status = EngineeringChangeStatus.Submitted;
        SubmittedAtUtc = utcNow;
    }

    /// <summary>
    /// Submitted -> Approved. Approval creates the new revision in the same transaction; this
    /// method only records the decision and the link to the revision that was created.
    /// </summary>
    public void Approve(Guid approvedRevisionId, string reviewer, string? comment, DateTime utcNow)
    {
        EnsureSubmitted();

        if (approvedRevisionId == Guid.Empty)
        {
            throw new DomainRuleViolationException(
                DomainErrorCodes.ApprovalRevisionRequired,
                "Approval must link to the revision it created.");
        }

        Status = EngineeringChangeStatus.Approved;
        ApprovedRevisionId = approvedRevisionId;
        DecidedBy = EnsureNotBlankActor(reviewer);
        DecidedAtUtc = utcNow;
        ReviewerComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
    }

    /// <summary>Submitted -> Rejected, with a non-blank reason (spec §5.4).</summary>
    public void Reject(string reviewer, string? reason, DateTime utcNow)
    {
        EnsureSubmitted();

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainRuleViolationException(
                DomainErrorCodes.RejectionReasonRequired,
                "Rejection requires a non-blank reason.");
        }

        Status = EngineeringChangeStatus.Rejected;
        DecidedBy = EnsureNotBlankActor(reviewer);
        DecidedAtUtc = utcNow;
        ReviewerComment = reason.Trim();
    }

    /// <summary>Approved -> Applied, called by the release use case in the same transaction.</summary>
    public void MarkApplied(DateTime utcNow)
    {
        if (Status != EngineeringChangeStatus.Approved)
        {
            throw new DomainConflictException(
                DomainErrorCodes.InvalidTransition,
                $"ECO {EcoNumber} is {Status}; only an approved ECO can be applied by releasing its revision.");
        }

        Status = EngineeringChangeStatus.Applied;
        DecidedAtUtc ??= utcNow;
    }

    /// <summary>
    /// The staleness rule from spec §5.4/§8: an ECO may be approved only if its base revision
    /// is still the product's current released revision. The caller (application service)
    /// supplies the current revision id; this aggregate does not query anything.
    /// </summary>
    public void EnsureBaseRevisionIsStillCurrent(Guid currentReleasedRevisionId)
    {
        if (currentReleasedRevisionId != BaseReleasedRevisionId)
        {
            throw new DomainConflictException(
                DomainErrorCodes.StaleBaseRevision,
                $"ECO {EcoNumber} was written against a released revision that is no longer current. " +
                "Create a new ECO against the latest released revision.");
        }
    }

    /// <summary>
    /// Builds the approved revision: a copy of the base revision's lines with this ECO's
    /// changes applied, in the `Approved` status, linked back to this ECO.
    ///
    /// Cross-aggregate checks that need Component rows (is the replacement active?) are the
    /// application service's job; everything that can be decided from the base revision and
    /// the ECO lines alone is decided here.
    /// </summary>
    public BomRevision BuildApprovedRevision(
        BomRevision baseRevision,
        string nextRevisionCode,
        string createdBy,
        DateTime utcNow)
    {
        EnsureSubmitted();

        if (baseRevision.Id != BaseReleasedRevisionId)
        {
            throw new DomainConflictException(
                DomainErrorCodes.StaleBaseRevision,
                $"ECO {EcoNumber} was created against a different base revision than the one supplied.");
        }

        var newLines = new List<BomLine>();

        foreach (var baseLine in baseRevision.Lines)
        {
            var change = _lines.FirstOrDefault(l => l.TargetComponentId == baseLine.ComponentId);

            if (change is null)
            {
                // Untouched by this ECO: carry the line across verbatim.
                newLines.Add(BomLine.Create(baseLine.ComponentId, baseLine.Quantity, baseLine.Unit));
            }
            else
            {
                newLines.Add(change.ApplyTo(baseLine));
            }
        }

        return BomRevision.CreateApproved(
            baseRevision.ProductId,
            nextRevisionCode,
            newLines,
            createdBy,
            utcNow,
            sourceEcoId: Id);
    }

    private static string EnsureTitle(string? title)
    {
        var normalised = (title ?? string.Empty).Trim();

        if (normalised.Length == 0)
        {
            throw new DomainRuleViolationException(DomainErrorCodes.BlankTitle, "ECO title cannot be blank.");
        }

        if (normalised.Length > MaxTitleLength)
        {
            throw new DomainRuleViolationException(DomainErrorCodes.NameTooLong, $"ECO title cannot exceed {MaxTitleLength} characters.");
        }

        return normalised;
    }

    private void EnsureDraft()
    {
        if (Status != EngineeringChangeStatus.Draft)
        {
            throw new DomainConflictException(
                DomainErrorCodes.InvalidTransition,
                $"ECO {EcoNumber} is {Status}; only a draft ECO can be modified or submitted.");
        }
    }

    private void EnsureSubmitted()
    {
        if (Status != EngineeringChangeStatus.Submitted)
        {
            throw new DomainConflictException(
                DomainErrorCodes.InvalidTransition,
                $"ECO {EcoNumber} is {Status}; only a submitted ECO can be approved or rejected.");
        }
    }

    private static string EnsureNotBlankActor(string? actor)
    {
        if (string.IsNullOrWhiteSpace(actor))
        {
            throw new DomainRuleViolationException(DomainErrorCodes.BlankActor, "An actor is required for this action.");
        }

        return actor.Trim();
    }
}
