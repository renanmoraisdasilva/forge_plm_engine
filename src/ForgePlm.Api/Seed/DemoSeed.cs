using System.Text.Json;
using ForgePlm.Domain.Audit;
using ForgePlm.Domain.BillOfMaterials;
using ForgePlm.Domain.Components;
using ForgePlm.Domain.Products;
using ForgePlm.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ForgePlm.Api.Seed;

/// <summary>
/// Deterministic seed data for the demo scenario (spec §10).
///
/// Idempotent by construction: every record carries a fixed GUID (A-12) and is checked by
/// primary key before insertion, so running it twice changes nothing. This is what makes
/// `Database.Migrate()` + seed on every startup safe (A-11).
///
/// The seeded story is the demo's starting point: SERVER-100 at released revision B, with
/// MB-020 as its motherboard. The ECO that replaces it with MB-021 is deliberately NOT
/// seeded — creating it is the first act of the demo (D-02c).
/// </summary>
public static class DemoSeed
{
    // Fixed identifiers. Chosen to be readable in logs and stable across machines, so a
    // demo URL like /products/{id} is the same for everyone.
    public static readonly Guid Server100Id = Guid.Parse("00000001-0000-0000-0000-000000000001");
    public static readonly Guid RevBId = Guid.Parse("00000002-0000-0000-0000-000000000002");

    public static readonly Guid Cpu001Id = Guid.Parse("00000010-0000-0000-0000-000000000010");
    public static readonly Guid Mb020Id = Guid.Parse("00000011-0000-0000-0000-000000000011");
    public static readonly Guid Mb021Id = Guid.Parse("00000012-0000-0000-0000-000000000012");
    public static readonly Guid Ram032Id = Guid.Parse("00000013-0000-0000-0000-000000000013");
    public static readonly Guid Ssd100Id = Guid.Parse("00000014-0000-0000-0000-000000000014");
    public static readonly Guid Nic010Id = Guid.Parse("00000015-0000-0000-0000-000000000015");
    public static readonly Guid Fan005Id = Guid.Parse("00000016-0000-0000-0000-000000000016");

    /// <summary>The audit rows describing the seeded baseline: the product, then its released revision.</summary>
    public static readonly Guid BaselineProductAuditId = Guid.Parse("00000003-0000-0000-0000-000000000003");
    public static readonly Guid BaselineRevisionAuditId = Guid.Parse("00000004-0000-0000-0000-000000000004");

    private static readonly DateTime BaselineEffectiveAtUtc = new(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);

    public static async Task ApplyAsync(ForgePlmDbContext db, CancellationToken cancellationToken = default)
    {
        // Components first: the BOM references them.
        await InsertIfMissing(db, Cpu001Id, id => Component.Create("CPU-001", "Xeon Processor", "Processor", ComponentLifecycleStatus.Active, BaselineEffectiveAtUtc, id), cancellationToken);
        await InsertIfMissing(db, Mb020Id, id => Component.Create("MB-020", "Server Motherboard", "Motherboard", ComponentLifecycleStatus.Active, BaselineEffectiveAtUtc, id), cancellationToken);
        await InsertIfMissing(db, Mb021Id, id => Component.Create("MB-021", "Updated Server Motherboard", "Motherboard", ComponentLifecycleStatus.Active, BaselineEffectiveAtUtc, id), cancellationToken);
        await InsertIfMissing(db, Ram032Id, id => Component.Create("RAM-032", "32GB Memory Module", "Memory", ComponentLifecycleStatus.Active, BaselineEffectiveAtUtc, id), cancellationToken);
        await InsertIfMissing(db, Ssd100Id, id => Component.Create("SSD-100", "Enterprise SSD 1TB", "Storage", ComponentLifecycleStatus.Active, BaselineEffectiveAtUtc, id), cancellationToken);
        await InsertIfMissing(db, Nic010Id, id => Component.Create("NIC-010", "10Gb Network Adapter", "Networking", ComponentLifecycleStatus.Active, BaselineEffectiveAtUtc, id), cancellationToken);
        await InsertIfMissing(db, Fan005Id, id => Component.Create("FAN-005", "Cooling Fan Assembly", "Thermal", ComponentLifecycleStatus.Active, BaselineEffectiveAtUtc, id), cancellationToken);

        await InsertIfMissing(db, Server100Id, id => Product.Create("SERVER-100", "Compute Server", "Server", ProductLifecycleStatus.Active, BaselineEffectiveAtUtc, id), cancellationToken);

        await InsertIfMissing(db, RevBId, CreateReleasedBaselineRevB, cancellationToken);

        // The baseline has two facts worth recording, and they belong to different entities:
        // the product was created, and revision B was released. Recording only "ProductCreated"
        // with a note about the release made the timeline claim the wrong event.
        await InsertIfMissing(
            db,
            BaselineProductAuditId,
            id => AuditEvent.Record(
                "System",
                AuditActions.ProductCreated,
                "Product",
                Server100Id,
                JsonSerializer.Serialize(new { note = "Seeded baseline product.", partNumber = "SERVER-100" }),
                BaselineEffectiveAtUtc,
                id),
            cancellationToken);

        await InsertIfMissing(
            db,
            BaselineRevisionAuditId,
            id => AuditEvent.Record(
                "System",
                AuditActions.RevisionReleased,
                "BomRevision",
                RevBId,
                JsonSerializer.Serialize(new { note = "Seeded baseline release.", revision = "B" }),
                BaselineEffectiveAtUtc,
                id),
            cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }

    private static BomRevision CreateReleasedBaselineRevB(Guid id) =>
        BomRevision.CreateReleasedBaseline(
            Server100Id,
            "B",
            [
                BomLine.Create(Cpu001Id, 2, "Each"),
                BomLine.Create(Mb020Id, 1, "Each"),
                BomLine.Create(Ram032Id, 8, "Each"),
                BomLine.Create(Ssd100Id, 2, "Each"),
                BomLine.Create(Nic010Id, 1, "Each"),
                BomLine.Create(Fan005Id, 4, "Each"),
            ],
            createdBy: "System",
            utcNow: BaselineEffectiveAtUtc,
            effectiveAtUtc: BaselineEffectiveAtUtc,
            id: id);

    /// <summary>
    /// Inserts an entity if its fixed id is not already present.
    ///
    /// The factory *receives* the id rather than closing over it. That is deliberate: the
    /// first version of this seed took a `Func<T>` and a separate id, and one call site passed
    /// the id only to the existence check while the factory minted a random one — so every
    /// startup inserted another copy of that row. Making the factory take the id makes that
    /// mistake unrepresentable (IT-08).
    /// </summary>
    private static async Task InsertIfMissing<T>(ForgePlmDbContext db, Guid id, Func<Guid, T> factory, CancellationToken cancellationToken)
        where T : class
    {
        // Checked by primary key: if a previous run inserted it, this run does nothing.
        // `EF.Property` keeps this generic without exposing Id on every entity type.
        var alreadySeeded = await db.Set<T>()
            .AnyAsync(e => EF.Property<Guid>(e, "Id") == id, cancellationToken);

        if (alreadySeeded)
        {
            return;
        }

        db.Add(factory(id));
    }
}
