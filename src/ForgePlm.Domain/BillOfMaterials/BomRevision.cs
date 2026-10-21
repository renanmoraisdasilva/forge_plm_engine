using ForgePlm.Domain.Common;

namespace ForgePlm.Domain.BillOfMaterials;

public enum BomRevisionStatus
{
    /// <summary>Defined for enum completeness only. Nothing in v1 creates a draft revision (A-18).</summary>
    Draft = 0,

    Approved = 1,
    Released = 2,
    Superseded = 3,
}

/// <summary>
/// An immutable snapshot of one product's configuration at one revision identifier (spec §5.3).
///
/// Immutability is enforced by construction: lines are supplied once, when the revision is
/// created, and no method exposes a way to change them afterwards. A released revision is
/// never mutated again — releasing the next revision marks it <see cref="Superseded"/>, which
/// changes its status and nothing else.
/// </summary>
public class BomRevision
{
    public const int MaxActorLength = 200;

    public Guid Id { get; private set; }

    public Guid ProductId { get; private set; }

    /// <summary>`A`, `B`, `C`, … assigned per product in sequence.</summary>
    public string RevisionCode { get; private set; } = string.Empty;

    public BomRevisionStatus Status { get; private set; }

    /// <summary>
    /// Set at release. Under D-04b this is a recorded label, not a trigger: the revision is
    /// current from the moment it is released, and a future timestamp means "scheduled",
    /// never "not yet effective". Nothing may filter on this to decide what is current.
    /// </summary>
    public DateTime? EffectiveAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public string CreatedBy { get; private set; } = string.Empty;

    /// <summary>Null only for the seeded baseline, which has no originating ECO.</summary>
    public Guid? SourceEcoId { get; private set; }

    private readonly List<BomLine> _lines = [];

    /// <summary>
    /// Read-only. Wrapping in `AsReadOnly()` rather than returning `_lines` typed as
    /// `IReadOnlyList`: a caller could otherwise cast back to `List&lt;BomLine&gt;` and mutate a
    /// released revision, which would break the "immutable forever" claim the demo rests on.
    /// EF populates the `_lines` field directly (see `HasField` + `PropertyAccessMode.Field`
    /// in the configuration), so the wrapper does not affect materialisation.
    /// </summary>
    public IReadOnlyList<BomLine> Lines => _lines.AsReadOnly();

    private BomRevision()
    {
        // EF Core materialisation only.
    }

    /// <summary>
    /// Creates a revision already in `Approved` status, which is the only way v1 ever
    /// creates one (spec §16). Used both by the seed and by ECO approval.
    /// </summary>
    /// <param name="id">Optional fixed identifier for deterministic seeding (A-12).</param>
    public static BomRevision CreateApproved(
        Guid productId,
        string? revisionCode,
        IEnumerable<BomLine> lines,
        string createdBy,
        DateTime utcNow,
        Guid? sourceEcoId,
        Guid? id = null)
    {
        var revision = new BomRevision
        {
            Id = id ?? Guid.NewGuid(),
            ProductId = productId,
            RevisionCode = Common.RevisionCode.EnsureValid(revisionCode),
            Status = BomRevisionStatus.Approved,
            CreatedAtUtc = utcNow,
            CreatedBy = Common.DomainText.Required(
                createdBy, MaxActorLength, "Created-by", DomainErrorCodes.BlankActor),
            SourceEcoId = sourceEcoId,
        };

        revision.SetLines(lines);

        return revision;
    }

    /// <summary>
    /// Creates a revision directly in `Released` status. Only the seed uses this, to
    /// establish the starting baseline without an ECO.
    /// </summary>
    /// <param name="id">Optional fixed identifier for deterministic seeding (A-12).</param>
    public static BomRevision CreateReleasedBaseline(
        Guid productId,
        string? revisionCode,
        IEnumerable<BomLine> lines,
        string createdBy,
        DateTime utcNow,
        DateTime effectiveAtUtc,
        Guid? id = null)
    {
        var revision = CreateApproved(productId, revisionCode, lines, createdBy, utcNow, sourceEcoId: null, id: id);
        revision.Status = BomRevisionStatus.Released;
        revision.EffectiveAtUtc = effectiveAtUtc;

        return revision;
    }

    /// <summary>
    /// Rule from spec §5.3: each component appears at most once per revision. Adding a
    /// second line for the same component is rejected rather than merged, so the UI never
    /// has to explain why two rows silently became one.
    /// </summary>
    private void SetLines(IEnumerable<BomLine> lines)
    {
        var seen = new HashSet<Guid>();

        foreach (var line in lines)
        {
            if (!seen.Add(line.ComponentId))
            {
                throw new DomainRuleViolationException(
                    DomainErrorCodes.DuplicateComponentInRevision,
                    "A component can appear at most once in a BOM revision.");
            }

            _lines.Add(line);
        }

        if (_lines.Count == 0)
        {
            // `bom-lines-required`, not `eco-lines-required`: this is a BOM revision with no
            // lines, a different aggregate from an ECO. Sharing one code made the taxonomy
            // lie about which object was at fault.
            throw new DomainRuleViolationException(
                DomainErrorCodes.BomLinesRequired,
                "A BOM revision must contain at least one line.");
        }
    }

    /// <summary>
    /// Returns the line for a component, or null. Used by the ECO approval logic to check
    /// that every target line actually exists in the revision being changed.
    /// </summary>
    public BomLine? FindLine(Guid componentId) => _lines.FirstOrDefault(l => l.ComponentId == componentId);

    /// <summary>
    /// Rule from spec §5.3: an approved or released revision cannot have its lines edited,
    /// and a released revision is immutable forever.
    /// </summary>
    public void EnsureLinesAreEditable()
    {
        if (Status != BomRevisionStatus.Draft)
        {
            throw new DomainConflictException(
                DomainErrorCodes.InvalidTransition,
                $"A {Status} BOM revision cannot be edited. Create a new revision instead.");
        }
    }

    /// <summary>
    /// Rule from spec §5.4/§8: only an `Approved` revision can be released, and only the
    /// revision linked to an ECO may be released through that ECO.
    /// </summary>
    public void Release(DateTime effectiveAtUtc)
    {
        if (Status != BomRevisionStatus.Approved)
        {
            throw new DomainConflictException(
                DomainErrorCodes.InvalidTransition,
                $"A {Status} BOM revision cannot be released; only an approved revision can.");
        }

        Status = BomRevisionStatus.Released;
        EffectiveAtUtc = effectiveAtUtc;
    }

    /// <summary>
    /// Called on the *previous* current released revision when a new one is released, inside
    /// the same transaction (spec §5.3). Superseded revisions stay fully queryable.
    /// </summary>
    public void MarkSuperseded()
    {
        if (Status != BomRevisionStatus.Released)
        {
            throw new DomainConflictException(
                DomainErrorCodes.InvalidTransition,
                $"Only a released revision can be superseded; this revision is {Status}.");
        }

        Status = BomRevisionStatus.Superseded;
    }
}
