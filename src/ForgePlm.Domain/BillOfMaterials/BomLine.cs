using ForgePlm.Domain.Common;

namespace ForgePlm.Domain.BillOfMaterials;

/// <summary>
/// One component and quantity inside one BOM revision (spec §5.3).
///
/// A line is owned by its revision and has no independent life: it is created with the
/// revision and is never edited or moved. The `BomRevisionId` back-reference exists for
/// EF Core's benefit, not as a domain concept.
/// </summary>
public class BomLine
{
    public const string DefaultUnit = "Each";

    public Guid Id { get; private set; }

    public Guid BomRevisionId { get; private set; }

    public Guid ComponentId { get; private set; }

    public int Quantity { get; private set; }

    public string Unit { get; private set; } = DefaultUnit;

    private BomLine()
    {
        // EF Core materialisation only.
    }

    /// <param name="id">Optional fixed identifier for deterministic seeding (A-12).</param>
    public static BomLine Create(Guid componentId, int quantity, string? unit, Guid? id = null)
    {
        if (componentId == Guid.Empty)
        {
            // `component-required`, not `blank-part-number`: a missing component reference is
            // not a part-number formatting problem, and the two are distinguishable to a client.
            throw new DomainRuleViolationException(
                DomainErrorCodes.ComponentRequired,
                "A BOM line must reference a component.");
        }

        return new BomLine
        {
            Id = id ?? Guid.NewGuid(),
            ComponentId = componentId,
            Quantity = Common.Quantity.From(quantity).Value,
            Unit = string.IsNullOrWhiteSpace(unit) ? DefaultUnit : unit.Trim(),
        };
    }
}
