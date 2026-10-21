using ForgePlm.Domain.BillOfMaterials;
using ForgePlm.Domain.Common;

namespace ForgePlm.Domain.EngineeringChanges;

/// <summary>
/// One proposed change to one BOM line (spec §5.4). Limited to replacing a component or
/// changing a quantity — the two change types the demo needs, and no generic change model.
/// </summary>
public class EngineeringChangeLine
{
    public Guid Id { get; private set; }

    public EngineeringChangeType ChangeType { get; private set; }

    /// <summary>The component being replaced or re-quantified.</summary>
    public Guid TargetComponentId { get; private set; }

    /// <summary>Required for `ReplaceComponent`.</summary>
    public Guid? ReplacementComponentId { get; private set; }

    /// <summary>Required for `ChangeQuantity`.</summary>
    public int? NewQuantity { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    private EngineeringChangeLine()
    {
        // EF Core materialisation only.
    }

    /// <param name="id">Optional fixed identifier for deterministic seeding (A-12).</param>
    public static EngineeringChangeLine Replace(Guid targetComponentId, Guid replacementComponentId, string? reason, Guid? id = null)
    {
        if (targetComponentId == Guid.Empty)
        {
            throw new DomainRuleViolationException(DomainErrorCodes.BlankPartNumber, "A change line must target a component.");
        }

        if (replacementComponentId == Guid.Empty)
        {
            throw new DomainRuleViolationException(DomainErrorCodes.BlankPartNumber, "A replacement component is required.");
        }

        if (targetComponentId == replacementComponentId)
        {
            throw new DomainRuleViolationException(
                DomainErrorCodes.ReplacementMustDiffer,
                "The replacement component must differ from the component it replaces.");
        }

        return new EngineeringChangeLine
        {
            Id = id ?? Guid.NewGuid(),
            ChangeType = EngineeringChangeType.ReplaceComponent,
            TargetComponentId = targetComponentId,
            ReplacementComponentId = replacementComponentId,
            Reason = (reason ?? string.Empty).Trim(),
        };
    }

    /// <param name="id">Optional fixed identifier for deterministic seeding (A-12).</param>
    public static EngineeringChangeLine ChangeQuantity(Guid targetComponentId, int newQuantity, string? reason, Guid? id = null)
    {
        if (targetComponentId == Guid.Empty)
        {
            throw new DomainRuleViolationException(DomainErrorCodes.BlankPartNumber, "A change line must target a component.");
        }

        return new EngineeringChangeLine
        {
            Id = id ?? Guid.NewGuid(),
            ChangeType = EngineeringChangeType.ChangeQuantity,
            TargetComponentId = targetComponentId,
            NewQuantity = Quantity.From(newQuantity).Value,
            Reason = (reason ?? string.Empty).Trim(),
        };
    }

    /// <summary>
    /// Rule from spec §5.4, checked at submission: the target must exist in the base revision
    /// this ECO was written against. Catching it here means a bad draft is caught early, and
    /// approval only has to worry about the world having moved on.
    /// </summary>
    public void EnsureTargetExistsIn(BomRevision baseRevision)
    {
        if (baseRevision.FindLine(TargetComponentId) is null)
        {
            throw new DomainConflictException(
                DomainErrorCodes.TargetNotInBaseRevision,
                $"The target component is not present in base revision {baseRevision.RevisionCode}, so it cannot be changed.");
        }
    }

    /// <summary>Applies this change to a base line, returning the new line. Never mutates the base line.</summary>
    public BomLine ApplyTo(BomLine baseLine)
    {
        if (baseLine.ComponentId != TargetComponentId)
        {
            throw new DomainConflictException(
                DomainErrorCodes.TargetNotInBaseRevision,
                "This change line does not target the supplied BOM line.");
        }

        return ChangeType switch
        {
            // Replacement: quantity defaults to the existing line's quantity (spec §5.4).
            EngineeringChangeType.ReplaceComponent =>
                BomLine.Create(ReplacementComponentId!.Value, baseLine.Quantity, baseLine.Unit),

            EngineeringChangeType.ChangeQuantity =>
                BomLine.Create(baseLine.ComponentId, NewQuantity!.Value, baseLine.Unit),

            _ => throw new DomainConflictException(DomainErrorCodes.InvalidTransition, $"Unknown change type {ChangeType}."),
        };
    }
}
