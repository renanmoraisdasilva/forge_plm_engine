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
    public static readonly Guid Cpu002Id = Guid.Parse("00000017-0000-0000-0000-000000000017");
    public static readonly Guid Cpu003Id = Guid.Parse("00000018-0000-0000-0000-000000000018");
    public static readonly Guid Cpu004Id = Guid.Parse("00000019-0000-0000-0000-000000000019");
    public static readonly Guid Mb022Id = Guid.Parse("0000001a-0000-0000-0000-00000000001a");
    public static readonly Guid Ram064Id = Guid.Parse("0000001b-0000-0000-0000-00000000001b");
    public static readonly Guid Ram128Id = Guid.Parse("0000001c-0000-0000-0000-00000000001c");
    public static readonly Guid Ssd200Id = Guid.Parse("0000001d-0000-0000-0000-00000000001d");
    public static readonly Guid Ssd400Id = Guid.Parse("0000001e-0000-0000-0000-00000000001e");
    public static readonly Guid Nic025Id = Guid.Parse("0000001f-0000-0000-0000-00000000001f");
    public static readonly Guid Nic100Id = Guid.Parse("00000020-0000-0000-0000-000000000020");
    public static readonly Guid Fan006Id = Guid.Parse("00000021-0000-0000-0000-000000000021");
    public static readonly Guid Fan007Id = Guid.Parse("00000022-0000-0000-0000-000000000022");

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
        await InsertIfMissing(db, Cpu002Id, id => Component.Create("CPU-002", "Intel Xeon Silver 12-Core Processor", "Processor", ComponentLifecycleStatus.Active, BaselineEffectiveAtUtc, id), cancellationToken);
        await InsertIfMissing(db, Cpu003Id, id => Component.Create("CPU-003", "Intel Xeon Gold 24-Core Processor", "Processor", ComponentLifecycleStatus.Active, BaselineEffectiveAtUtc, id), cancellationToken);
        await InsertIfMissing(db, Cpu004Id, id => Component.Create("CPU-004", "AMD EPYC 32-Core Processor", "Processor", ComponentLifecycleStatus.Active, BaselineEffectiveAtUtc, id), cancellationToken);
        await InsertIfMissing(db, Mb022Id, id => Component.Create("MB-022", "Dual-Socket Xeon Server Motherboard", "Motherboard", ComponentLifecycleStatus.Active, BaselineEffectiveAtUtc, id), cancellationToken);
        await InsertIfMissing(db, Ram064Id, id => Component.Create("RAM-064", "64GB DDR5 ECC Registered Memory Module", "Memory", ComponentLifecycleStatus.Active, BaselineEffectiveAtUtc, id), cancellationToken);
        await InsertIfMissing(db, Ram128Id, id => Component.Create("RAM-128", "128GB DDR5 ECC Registered Memory Module", "Memory", ComponentLifecycleStatus.Active, BaselineEffectiveAtUtc, id), cancellationToken);
        await InsertIfMissing(db, Ssd200Id, id => Component.Create("SSD-200", "Enterprise NVMe SSD 2TB", "Storage", ComponentLifecycleStatus.Active, BaselineEffectiveAtUtc, id), cancellationToken);
        await InsertIfMissing(db, Ssd400Id, id => Component.Create("SSD-400", "Enterprise NVMe SSD 4TB", "Storage", ComponentLifecycleStatus.Active, BaselineEffectiveAtUtc, id), cancellationToken);
        await InsertIfMissing(db, Nic025Id, id => Component.Create("NIC-025", "Dual-Port 25GbE Network Adapter", "Networking", ComponentLifecycleStatus.Active, BaselineEffectiveAtUtc, id), cancellationToken);
        await InsertIfMissing(db, Nic100Id, id => Component.Create("NIC-100", "Dual-Port 100GbE Network Adapter", "Networking", ComponentLifecycleStatus.Active, BaselineEffectiveAtUtc, id), cancellationToken);
        await InsertIfMissing(db, Fan006Id, id => Component.Create("FAN-006", "Hot-Swap High-Pressure Cooling Fan", "Thermal", ComponentLifecycleStatus.Active, BaselineEffectiveAtUtc, id), cancellationToken);
        await InsertIfMissing(db, Fan007Id, id => Component.Create("FAN-007", "Redundant High-Performance Cooling Fan", "Thermal", ComponentLifecycleStatus.Active, BaselineEffectiveAtUtc, id), cancellationToken);

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
