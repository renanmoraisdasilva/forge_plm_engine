using ForgePlm.Domain.BillOfMaterials;
using ForgePlm.Domain.Common;
using ForgePlm.Domain.EngineeringChanges;

namespace ForgePlm.Tests;

/// <summary>
/// Builders for domain objects in the shape the seed data uses, so tests read like the
/// demo scenario (spec §10) rather than like construction boilerplate.
/// </summary>
internal static class TestData
{
    public static readonly Guid Motherboard020 = Guid.NewGuid();
    public static readonly Guid Motherboard021 = Guid.NewGuid();
    public static readonly Guid Processor001 = Guid.NewGuid();
    public static readonly Guid Memory032 = Guid.NewGuid();

    public static readonly DateTime Now = new(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>The SERVER-100 revision B baseline: two untouched lines plus the motherboard.</summary>
    public static BomRevision Server100RevB(Guid productId) =>
        BomRevision.CreateReleasedBaseline(
            productId,
            "B",
            [
                BomLine.Create(Processor001, 2, "Each"),
                BomLine.Create(Motherboard020, 1, "Each"),
                BomLine.Create(Memory032, 8, "Each"),
            ],
            createdBy: "Alex Morgan",
            utcNow: Now.AddDays(-30),
            effectiveAtUtc: Now.AddDays(-30));

    /// <summary>The demo ECO: replace MB-020 with MB-021.</summary>
    public static EngineeringChangeOrder ReplaceMotherboardEco(Guid productId, BomRevision baseRevision)
    {
        var eco = EngineeringChangeOrder.Create(
            "ECO-2026-0042",
            productId,
            baseRevision.Id,
            "Replace motherboard controller",
            "Updated controller specification and improved component availability.",
            "The server BOM is affected. Existing released revision B remains available for historical reference.",
            requestedEffectiveAtUtc: Now.AddDays(11),
            createdBy: "Alex Morgan",
            utcNow: Now);

        eco.ReplaceLines([EngineeringChangeLine.Replace(Motherboard020, Motherboard021, "Controller spec update")]);

        return eco;
    }
}
