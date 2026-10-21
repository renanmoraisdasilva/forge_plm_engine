using ForgePlm.Api.Persistence;
using ForgePlm.Api.Seed;
using ForgePlm.Domain.Audit;
using ForgePlm.Domain.BillOfMaterials;
using ForgePlm.Domain.Components;
using ForgePlm.Domain.EngineeringChanges;
using ForgePlm.Domain.Products;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ForgePlm.Tests.Integration;

/// <summary>
/// Persistence behaviour that the schema is responsible for (spec §11, ADR-004).
///
/// These run against the real PostgreSQL container. Anything a test here "proves" about
/// transactions, uniqueness or concurrency would be meaningless on the in-memory provider,
/// which §11 explicitly forbids for such claims.
///
/// Each test gets its own database, so no test's state can leak into another regardless of
/// execution order.
/// </summary>
[Collection(PostgresCollection.Name)]
public class PersistenceTests(PostgresFixture fixture)
{
    /// <summary>
    /// IT-08 — seed data is idempotent: a second application changes nothing.
    ///
    /// This caught a real bug during Slice 2. The seed helper took an id for the existence
    /// check and a separate factory that minted its own Guid, so the audit row was re-inserted
    /// on every startup: 7 components (correct, they passed the id through) but 2, then 3,
    /// audit events. The fix made the factory receive the id, so the mistake is unrepresentable.
    /// </summary>
    [Fact]
    public async Task IT08_SeedingTwiceChangesNothing()
    {
        await using var database = await fixture.CreateDatabaseAsync();

        await database.SeedAsync();
        var afterFirst = await SnapshotAsync(database);

        await database.SeedAsync();
        var afterSecond = await SnapshotAsync(database);

        Assert.Equal(afterFirst, afterSecond);
    }

    /// <summary>IT-08's companion: the seed produces the demo scenario exactly as spec §10 asks.</summary>
    [Fact]
    public async Task IT08_SeedProducesTheDemoBaseline()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await database.SeedAsync();

        await using var db = database.CreateDbContext();

        var product = await db.Products.SingleAsync();
        Assert.Equal("SERVER-100", product.PartNumber);
        Assert.Equal(ProductLifecycleStatus.Active, product.LifecycleStatus);

        Assert.Equal(7, await db.Components.CountAsync());

        var revision = await db.BomRevisions.SingleAsync();
        Assert.Equal("B", revision.RevisionCode);
        Assert.Equal(BomRevisionStatus.Released, revision.Status);
        Assert.Equal(6, await db.BomLines.CountAsync(l => l.BomRevisionId == revision.Id));
    }

    /// <summary>
    /// The baseline records the two facts that actually happened: the product was created and
    /// revision B was released. Recording only `ProductCreated` with a release note made the
    /// timeline show the wrong event and no release at all.
    /// </summary>
    [Fact]
    public async Task IT08_BaselineAuditSeparatesProductCreationFromRevisionRelease()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await database.SeedAsync();

        await using var db = database.CreateDbContext();

        Assert.True(await db.AuditEvents.AnyAsync(audit =>
            audit.Action == AuditActions.ProductCreated &&
            audit.EntityType == "Product" &&
            audit.EntityId == DemoSeed.Server100Id));

        Assert.True(await db.AuditEvents.AnyAsync(audit =>
            audit.Action == AuditActions.RevisionReleased &&
            audit.EntityType == "BomRevision" &&
            audit.EntityId == DemoSeed.RevBId));
    }

    /// <summary>
    /// A BOM line is a reference to a component, so the schema refuses an orphan. Before the
    /// foreign key, an orphan was invisible to the inner-join read while still counted in
    /// `total` and `lineCount` — a silent inconsistency.
    /// </summary>
    [Fact]
    public async Task ABomLineMustReferenceAnExistingComponent()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await database.SeedAsync();

        await using var db = database.CreateDbContext();
        var product = await db.Products.SingleAsync();

        db.BomRevisions.Add(BomRevision.CreateApproved(
            product.Id,
            "C",
            [BomLine.Create(Guid.NewGuid(), 1, "Each")],
            createdBy: "Test",
            utcNow: DateTime.UtcNow,
            sourceEcoId: null));

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        var postgres = Assert.IsType<PostgresException>(ex.InnerException);

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, postgres.SqlState);
    }

    /// <summary>An ECO line's target is a real component reference, not an unchecked Guid.</summary>
    [Fact]
    public async Task AnEcoLineMustReferenceAnExistingComponent()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await database.SeedAsync();

        await using var db = database.CreateDbContext();
        var product = await db.Products.SingleAsync();
        var baseRevision = await db.BomRevisions.SingleAsync(r => r.Status == BomRevisionStatus.Released);

        var eco = EngineeringChangeOrder.Create(
            "ECO-2026-0091",
            product.Id,
            baseRevision.Id,
            "Orphan reference",
            "Prove the component FK.",
            null,
            null,
            "Test",
            DateTime.UtcNow);
        eco.ReplaceLines([EngineeringChangeLine.ChangeQuantity(Guid.NewGuid(), 2, null)]);

        db.EngineeringChangeOrders.Add(eco);

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        var postgres = Assert.IsType<PostgresException>(ex.InnerException);

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, postgres.SqlState);
    }

    /// <summary>
    /// The duplicate-target domain rule is also a unique index, so a write that bypasses the
    /// aggregate cannot create two changes for the same component.
    /// </summary>
    [Fact]
    public async Task AnEcoCannotHaveTwoLinesForTheSameTargetInTheSchema()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await database.SeedAsync();

        await using var db = database.CreateDbContext();
        var product = await db.Products.SingleAsync();
        var baseRevision = await db.BomRevisions.SingleAsync(r => r.Status == BomRevisionStatus.Released);

        var eco = EngineeringChangeOrder.Create(
            "ECO-2026-0092",
            product.Id,
            baseRevision.Id,
            "Duplicate target",
            "Prove the composite index.",
            null,
            null,
            "Test",
            DateTime.UtcNow);
        eco.ReplaceLines([EngineeringChangeLine.ChangeQuantity(DemoSeed.Ram032Id, 16, null)]);
        db.EngineeringChangeOrders.Add(eco);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO engineering_change_lines
                (id, change_type, target_component_id, new_quantity, reason, engineering_change_order_id)
            VALUES ({0}, {1}, {2}, {3}, {4}, {5})
            """,
            Guid.NewGuid(),
            nameof(EngineeringChangeType.ChangeQuantity),
            DemoSeed.Ram032Id,
            8,
            string.Empty,
            eco.Id));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, ex.SqlState);
        Assert.Contains("ux_engineering_change_lines_eco_target", ex.Message);
    }

    /// <summary>The parent link is now required, so an ECO line cannot outlive its ECO.</summary>
    [Fact]
    public async Task AnEcoLineCannotExistWithoutAnEco()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await database.SeedAsync();

        await using var db = database.CreateDbContext();

        var ex = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO engineering_change_lines
                (id, change_type, target_component_id, new_quantity, reason, engineering_change_order_id)
            VALUES ({0}, {1}, {2}, {3}, {4}, NULL)
            """,
            Guid.NewGuid(),
            nameof(EngineeringChangeType.ChangeQuantity),
            DemoSeed.Ram032Id,
            2,
            string.Empty));

        Assert.Equal(PostgresErrorCodes.NotNullViolation, ex.SqlState);
    }

    /// <summary>
    /// IT-09 — a duplicate part number is refused by the database, and the domain's case
    /// normalisation is what makes that refusal case-insensitive in practice.
    ///
    /// These are two separate facts, and the distinction matters:
    ///
    ///   * the index is a plain unique index on the stored column, so it is case-*sensitive*;
    ///   * the domain normalises to upper case at every entry point, so `server-100` and
    ///     `SERVER-100` reach the column as the same value and collide.
    ///
    /// An earlier version of this test asserted that the *index* caught a case variant written
    /// directly through EF. It did not — no exception was thrown — which is exactly why the
    /// normalisation has to live in the domain rather than being assumed of the schema.
    /// </summary>
    [Fact]
    public async Task IT09_CaseVariantPartNumbersCollideBecauseTheDomainNormalises()
    {
        await using var database = await fixture.CreateDatabaseAsync();

        await using var db = database.CreateDbContext();
        db.Products.Add(Product.Create("SERVER-100", "Compute Server", "Server", ProductLifecycleStatus.Active, DateTime.UtcNow));
        await db.SaveChangesAsync();

        // A different case and padding, entering through the domain exactly as the API would.
        var duplicate = Product.Create("  server-100  ", "Duplicate", "Server", ProductLifecycleStatus.Active, DateTime.UtcNow);

        Assert.Equal("SERVER-100", duplicate.PartNumber);

        db.Products.Add(duplicate);

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(ex.InnerException).SqlState);
    }

    /// <summary>
    /// IT-09's second half — the constraint genuinely lives in the schema, not only in the
    /// domain. Writing the same value twice while bypassing the factories entirely is still
    /// refused, so a future endpoint that skipped the domain could not silently fork a part.
    /// </summary>
    [Fact]
    public async Task IT09_PartNumberUniquenessIsEnforcedByTheDatabaseItself()
    {
        await using var database = await fixture.CreateDatabaseAsync();

        await using var db = database.CreateDbContext();
        db.Products.Add(Product.Create("SERVER-100", "Compute Server", "Server", ProductLifecycleStatus.Active, DateTime.UtcNow));
        await db.SaveChangesAsync();

        var duplicate = Product.Create("SERVER-100-DUPLICATE", "Duplicate", "Server", ProductLifecycleStatus.Active, DateTime.UtcNow);

        // Overwrite the stored value after construction, so the insert is a raw write rather
        // than something the domain would have normalised or rejected.
        typeof(Product).GetProperty(nameof(Product.PartNumber))!.SetValue(duplicate, "SERVER-100");
        db.Products.Add(duplicate);

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        var postgres = Assert.IsType<PostgresException>(ex.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
        Assert.Contains("ux_products_part_number", postgres.Message);
    }

    /// <summary>
    /// IT-10 — the partial unique index refuses a second `Released` revision for one product.
    ///
    /// ADR-004's second line of defence. The application logic in slice 5/6 prevents this too,
    /// but the schema must refuse it regardless, because that is the invariant that actually
    /// protects the demo's historical-integrity claim.
    /// </summary>
    [Fact]
    public async Task IT10_OnlyOneReleasedRevisionPerProductIsAllowedByTheSchema()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await database.SeedAsync();

        await using var db = database.CreateDbContext();
        var product = await db.Products.SingleAsync();
        var component = await db.Components.FirstAsync();

        // A second revision in `Released` status for the same product, inserted directly so the
        // test exercises the index rather than the aggregate's release() method.
        var second = BomRevision.CreateReleasedBaseline(
            product.Id,
            "C",
            [BomLine.Create(component.Id, 1, "Each")],
            createdBy: "Test",
            utcNow: DateTime.UtcNow,
            effectiveAtUtc: DateTime.UtcNow);

        db.BomRevisions.Add(second);

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        var postgres = Assert.IsType<PostgresException>(ex.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
        Assert.Contains("ux_bom_revisions_one_released_per_product", postgres.Message);
    }

    /// <summary>IT-10's positive case: an `Approved` revision may coexist with a `Released` one.</summary>
    [Fact]
    public async Task AnApprovedRevisionMayCoexistWithAReleasedOne()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await database.SeedAsync();

        await using var db = database.CreateDbContext();
        var product = await db.Products.SingleAsync();
        var component = await db.Components.FirstAsync();

        var approved = BomRevision.CreateApproved(
            product.Id,
            "C",
            [BomLine.Create(component.Id, 1, "Each")],
            createdBy: "Test",
            utcNow: DateTime.UtcNow,
            sourceEcoId: Guid.NewGuid());

        db.BomRevisions.Add(approved);
        await db.SaveChangesAsync();

        Assert.Equal(2, await db.BomRevisions.CountAsync(r => r.ProductId == product.Id));
    }

    /// <summary>
    /// A duplicate revision code for the same product is refused, so `A → B → C` cannot fork.
    /// </summary>
    [Fact]
    public async Task DuplicateRevisionCodeForTheSameProductIsRefused()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await database.SeedAsync();

        await using var db = database.CreateDbContext();
        var product = await db.Products.SingleAsync();
        var component = await db.Components.FirstAsync();

        db.BomRevisions.Add(BomRevision.CreateApproved(
            product.Id,
            "B",
            [BomLine.Create(component.Id, 1, "Each")],
            createdBy: "Test",
            utcNow: DateTime.UtcNow,
            sourceEcoId: Guid.NewGuid()));

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        var postgres = Assert.IsType<PostgresException>(ex.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
        Assert.Contains("ux_bom_revisions_product_revision_code", postgres.Message);
    }

    /// <summary>
    /// IT-11 — the release ordering question from the 2026-10-21 code review, measured against
    /// real PostgreSQL rather than assumed either way.
    ///
    /// The partial unique index (`status = 'Released'`) is evaluated per statement and cannot be
    /// deferred: PostgreSQL only allows `DEFERRABLE` on table constraints, and a *partial* index
    /// is not one. So ordering is load-bearing — releasing the new revision while the old one is
    /// still `Released` is a 23505 no matter how the transaction is framed.
    ///
    /// This test proves the constraint holds for the real release-time shape (both revisions
    /// already persisted), which IT-10 does not: IT-10 conflicts on a fresh INSERT, this one
    /// conflicts on a second UPDATE setting `Released`.
    /// </summary>
    [Fact]
    public async Task IT11_ReleasingTheNewRevisionBeforeSupersedingTheOldOneViolatesTheIndex()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await database.SeedAsync();

        await using (var setup = database.CreateDbContext())
        {
            var product = await setup.Products.SingleAsync();
            var component = await setup.Components.FirstAsync();

            setup.BomRevisions.Add(BomRevision.CreateApproved(
                product.Id,
                "C",
                [BomLine.Create(component.Id, 1, "Each")],
                createdBy: "Test",
                utcNow: DateTime.UtcNow,
                sourceEcoId: Guid.NewGuid()));
            await setup.SaveChangesAsync();
        }

        // Wrong order on purpose: release the new revision while the old one is still Released.
        await using var db = database.CreateDbContext();
        var old = await db.BomRevisions.SingleAsync(r => r.Status == BomRevisionStatus.Released);
        var next = await db.BomRevisions.SingleAsync(r => r.Status == BomRevisionStatus.Approved);

        next.Release(DateTime.UtcNow);

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        var postgres = Assert.IsType<PostgresException>(ex.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
        Assert.Contains("ux_bom_revisions_one_released_per_product", postgres.Message);

        // And the old revision is untouched — the failure rolled the whole batch back.
        await using var verify = database.CreateDbContext();
        Assert.Equal(BomRevisionStatus.Released, (await verify.BomRevisions.SingleAsync(r => r.Id == old.Id)).Status);
        Assert.Equal(BomRevisionStatus.Approved, (await verify.BomRevisions.SingleAsync(r => r.Id == next.Id)).Status);
    }

    /// <summary>
    /// IT-11's positive case, and the contract Slice 6's release use case has to honour:
    /// supersede the old revision, save, release the new one, save — all inside one explicit
    /// transaction.
    ///
    /// Two statements in a fixed order rather than one unordered batch. The transaction keeps
    /// the pair atomic, so a failure on the second statement rolls the first one back and the
    /// product is never left with zero released revisions.
    ///
    /// <para>
    /// <b>Why not just let EF batch it.</b> The review predicted a single `SaveChanges` would
    /// fail. Measured on 2026-10-21 against EF Core 10 + PostgreSQL 18: it did <b>not</b> fail,
    /// stably across five runs — EF happened to order the supersede before the release. That is
    /// an accident of batching, not a contract: EF Core guarantees ordering only for dependent
    /// commands, and two updates to one table carry no such guarantee. So the ordered design is
    /// still what Slice 6 ships, but the review's specific prediction was wrong and this comment
    /// says so rather than quietly agreeing with it.
    /// </para>
    /// </summary>
    [Fact]
    public async Task IT11_OrderedTwoStepReleasePersistsAtomicallyInOneTransaction()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await database.SeedAsync();

        await using (var setup = database.CreateDbContext())
        {
            var product = await setup.Products.SingleAsync();
            var component = await setup.Components.FirstAsync();

            setup.BomRevisions.Add(BomRevision.CreateApproved(
                product.Id,
                "C",
                [BomLine.Create(component.Id, 1, "Each")],
                createdBy: "Test",
                utcNow: DateTime.UtcNow,
                sourceEcoId: Guid.NewGuid()));
            await setup.SaveChangesAsync();
        }

        await using var db = database.CreateDbContext();
        var old = await db.BomRevisions.SingleAsync(r => r.Status == BomRevisionStatus.Released);
        var next = await db.BomRevisions.SingleAsync(r => r.Status == BomRevisionStatus.Approved);

        await using (await db.Database.BeginTransactionAsync())
        {
            old.MarkSuperseded();
            await db.SaveChangesAsync();

            next.Release(DateTime.UtcNow);
            await db.SaveChangesAsync();

            await db.Database.CommitTransactionAsync();
        }

        // Verified against a fresh context so the assertions read the database, not the
        // change tracker.
        await using var verify = database.CreateDbContext();
        var oldReloaded = await verify.BomRevisions.SingleAsync(r => r.Id == old.Id);
        var nextReloaded = await verify.BomRevisions.SingleAsync(r => r.Id == next.Id);

        Assert.Equal(BomRevisionStatus.Superseded, oldReloaded.Status);
        Assert.Equal(BomRevisionStatus.Released, nextReloaded.Status);
        Assert.Equal(1, await verify.BomRevisions.CountAsync(r => r.ProductId == old.ProductId && r.Status == BomRevisionStatus.Released));
    }

    private static async Task<string> SnapshotAsync(IsolatedDatabase database)
    {
        await using var db = database.CreateDbContext();

        var components = await db.Components.OrderBy(c => c.Id).Select(c => c.Id).ToListAsync();
        var products = await db.Products.OrderBy(p => p.Id).Select(p => p.Id).ToListAsync();
        var revisions = await db.BomRevisions.OrderBy(r => r.Id).Select(r => r.Id).ToListAsync();
        var lines = await db.BomLines.OrderBy(l => l.Id).Select(l => l.Id).ToListAsync();
        var audit = await db.AuditEvents.OrderBy(a => a.Id).Select(a => a.Id).ToListAsync();

        // Ids only: if the seed inserted a duplicate row with a fresh Guid, the count and the
        // id list both change, which is precisely the failure mode this test exists to catch.
        return string.Join("|", components.Select(i => i.ToString()))
             + "#" + string.Join("|", products.Select(i => i.ToString()))
             + "#" + string.Join("|", revisions.Select(i => i.ToString()))
             + "#" + string.Join("|", lines.Select(i => i.ToString()))
             + "#" + string.Join("|", audit.Select(i => i.ToString()));
    }
}

