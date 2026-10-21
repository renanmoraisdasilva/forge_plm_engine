using System.Text.Json;
using ForgePlm.Api.Application;
using ForgePlm.Api.Seed;
using ForgePlm.Domain.Audit;
using ForgePlm.Domain.BillOfMaterials;
using ForgePlm.Domain.Common;
using ForgePlm.Domain.EngineeringChanges;
using ForgePlm.Domain.Products;
using Microsoft.EntityFrameworkCore;

namespace ForgePlm.Tests.Integration;

/// <summary>
/// Slices 4–6 at the use-case level, against real PostgreSQL (spec §11).
///
/// These drive the same application services the endpoints call, so the transaction boundaries
/// and the schema are the ones the demo uses. Assertions read from a fresh context wherever the
/// point is "what was persisted", never from the change tracker.
///
/// IT-06 and IT-07 are here rather than in the API tests on purpose: rollback and optimistic
/// concurrency are relational behaviour, and spec §11 forbids claiming them from anything but
/// the real engine.
/// </summary>
[Collection(PostgresCollection.Name)]
public class EcoWorkflowTests(PostgresFixture fixture)
{
    private static NewEco ReplaceMotherboardEco(string title = "Replace motherboard controller") => new(
        DemoSeed.Server100Id,
        title,
        "Updated controller specification and improved component availability.",
        "Existing released revision B remains available for historical reference.",
        DateTime.UtcNow.AddDays(11),
        "Alex Morgan",
        [new NewEcoLine(EngineeringChangeType.ReplaceComponent, DemoSeed.Mb020Id, DemoSeed.Mb021Id, null, "Controller spec update")]);

    private static async Task<EngineeringChangeOrder> CreateAsync(IsolatedDatabase database, string title)
    {
        await using var db = database.CreateDbContext();
        var eco = await new CreateEngineeringChange(db).ExecuteAsync(ReplaceMotherboardEco(title));

        Assert.NotNull(eco);
        return eco;
    }

    private static async Task SubmitAsync(IsolatedDatabase database, Guid ecoId)
    {
        await using var db = database.CreateDbContext();
        var eco = await new SubmitEngineeringChange(db).ExecuteAsync(ecoId, "Alex Morgan");

        Assert.NotNull(eco);
        Assert.Equal(EngineeringChangeStatus.Submitted, eco.Status);
    }

    private static async Task<EngineeringChangeOrder> ApproveAsync(IsolatedDatabase database, Guid ecoId)
    {
        await using var db = database.CreateDbContext();
        var eco = await new ApproveEngineeringChange(db).ExecuteAsync(ecoId, "Jordan Lee", "Validated on test bench.");

        Assert.NotNull(eco);
        return eco;
    }

    /// <summary>IT-01 — creating an ECO captures the product's current released revision.</summary>
    [Fact]
    public async Task IT01_CreatingAnEcoCapturesTheCurrentReleasedRevision()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await database.SeedAsync();

        var eco = await CreateAsync(database, "Replace motherboard controller");

        Assert.Equal(DemoSeed.Server100Id, eco.ProductId);
        Assert.Equal(DemoSeed.RevBId, eco.BaseReleasedRevisionId);
        Assert.Equal(EngineeringChangeStatus.Draft, eco.Status);
        Assert.Equal("Alex Morgan", eco.CreatedBy);
        Assert.Single(eco.Lines);

        // A-04: server-generated, zero-padded, and this is the first ECO of the year.
        Assert.Matches(@"^ECO-\d{4}-0001$", eco.EcoNumber);

        await using var verify = database.CreateDbContext();
        var createdAudit = await verify.AuditEvents.AsNoTracking()
            .SingleAsync(audit => audit.Action == AuditActions.EcoCreated);

        Assert.Equal(eco.Id, createdAudit.EntityId);
        Assert.Equal("Alex Morgan", createdAudit.Actor);
    }

    /// <summary>A second creation takes the next number rather than colliding or reusing one.</summary>
    [Fact]
    public async Task IT01_EcoNumbersAdvanceWithinTheYear()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await database.SeedAsync();

        var first = await CreateAsync(database, "First change");
        var second = await CreateAsync(database, "Second change");

        Assert.NotEqual(first.EcoNumber, second.EcoNumber);
        Assert.EndsWith("-0001", first.EcoNumber);
        Assert.EndsWith("-0002", second.EcoNumber);
    }

    /// <summary>
    /// Creating against a product with no released BOM is a conflict with its own code, not a
    /// null base revision or a random 500.
    /// </summary>
    [Fact]
    public async Task CreatingForAProductWithoutAReleasedRevisionIsAConflict()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await database.SeedAsync();

        var product = Product.Create("NOBASE-1", "Product without a BOM", "Server", ProductLifecycleStatus.Active, DateTime.UtcNow);

        await using (var seedDb = database.CreateDbContext())
        {
            seedDb.Products.Add(product);
            await seedDb.SaveChangesAsync();
        }

        await using var db = database.CreateDbContext();
        var command = ReplaceMotherboardEco() with { ProductId = product.Id };

        var ex = await Assert.ThrowsAsync<DomainConflictException>(
            () => new CreateEngineeringChange(db).ExecuteAsync(command));

        Assert.Equal(DomainErrorCodes.NoReleasedRevision, ex.Code);
    }

    [Fact]
    public async Task InitialBomMakesANewProductAvailableToTheEcoWorkflow()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await database.SeedAsync();

        var product = Product.Create("NEW-SERVER-1", "New server", "Server", ProductLifecycleStatus.Active, DateTime.UtcNow);
        await using (var seedDb = database.CreateDbContext())
        {
            seedDb.Products.Add(product);
            await seedDb.SaveChangesAsync();
        }

        await using (var db = database.CreateDbContext())
        {
            var initial = await new CreateInitialBom(db).ExecuteAsync(product.Id, new NewInitialBom(
                [new NewInitialBomLine(DemoSeed.Mb020Id, 1, "Each")], "Alex Morgan"));

            Assert.NotNull(initial);
            Assert.Equal("A", initial.RevisionCode);
            Assert.Equal(BomRevisionStatus.Released, initial.Status);
            Assert.Equal(DateTimeKind.Utc, initial.EffectiveAtUtc!.Value.Kind);
            Assert.Equal(DateTimeKind.Utc, initial.ReleasedAtUtc!.Value.Kind);
            Assert.Single(initial.Lines);
        }

        var ecoCommand = ReplaceMotherboardEco() with { ProductId = product.Id };
        await using (var db = database.CreateDbContext())
        {
            var eco = await new CreateEngineeringChange(db).ExecuteAsync(ecoCommand);
            Assert.NotNull(eco);
            Assert.Equal("A", (await db.BomRevisions.AsNoTracking().SingleAsync(r => r.Id == eco.BaseReleasedRevisionId)).RevisionCode);
        }

        await using (var db = database.CreateDbContext())
        {
            var duplicate = await Assert.ThrowsAsync<DomainConflictException>(() =>
                new CreateInitialBom(db).ExecuteAsync(product.Id, new NewInitialBom(
                    [new NewInitialBomLine(DemoSeed.Mb020Id, 1, "Each")], "Alex Morgan")));
            Assert.Equal(DomainErrorCodes.InvalidTransition, duplicate.Code);
        }

        await using var verify = database.CreateDbContext();
        var audit = await verify.AuditEvents.AsNoTracking()
            .SingleAsync(a => a.Action == AuditActions.InitialRevisionReleased && a.EntityType == "BomRevision");
        Assert.Equal("Alex Morgan", audit.Actor);
    }

    /// <summary>
    /// IT-02 and IT-03 — approval creates the new revision with the requested change applied,
    /// while the old released revision remains byte-for-byte unchanged and still `Released`.
    /// </summary>
    [Fact]
    public async Task IT02_IT03_ApprovalCreatesTheChangedRevisionAndLeavesTheBaseUntouched()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await database.SeedAsync();

        var eco = await CreateAsync(database, "Replace motherboard controller");
        await SubmitAsync(database, eco.Id);
        var approved = await ApproveAsync(database, eco.Id);

        Assert.Equal(EngineeringChangeStatus.Approved, approved.Status);
        Assert.Equal("Jordan Lee", approved.DecidedBy);
        Assert.NotNull(approved.ApprovedRevisionId);

        await using var verify = database.CreateDbContext();

        var newRevision = await verify.BomRevisions.AsNoTracking()
            .Include(revision => revision.Lines)
            .SingleAsync(revision => revision.Id == approved.ApprovedRevisionId);

        Assert.Equal("C", newRevision.RevisionCode);
        Assert.Equal(BomRevisionStatus.Approved, newRevision.Status);
        Assert.Null(newRevision.EffectiveAtUtc);
        Assert.Equal(eco.Id, newRevision.SourceEcoId);
        Assert.Equal(6, newRevision.Lines.Count);
        Assert.Contains(newRevision.Lines, line => line.ComponentId == DemoSeed.Mb021Id && line.Quantity == 1);
        Assert.DoesNotContain(newRevision.Lines, line => line.ComponentId == DemoSeed.Mb020Id);

        // IT-03: the base is not mutated by approval — it is still the current released BOM.
        var baseRevision = await verify.BomRevisions.AsNoTracking()
            .Include(revision => revision.Lines)
            .SingleAsync(revision => revision.Id == DemoSeed.RevBId);

        Assert.Equal(BomRevisionStatus.Released, baseRevision.Status);
        Assert.Equal(new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc), baseRevision.EffectiveAtUtc);
        Assert.Equal(6, baseRevision.Lines.Count);
        Assert.Contains(baseRevision.Lines, line => line.ComponentId == DemoSeed.Mb020Id);
        Assert.DoesNotContain(baseRevision.Lines, line => line.ComponentId == DemoSeed.Mb021Id);

        // Approval wrote its audit row in the same transaction.
        Assert.True(await verify.AuditEvents.AsNoTracking()
            .AnyAsync(audit => audit.Action == AuditActions.EcoApproved && audit.EntityId == eco.Id));
    }

    /// <summary>
    /// IT-04 — release supersedes the previous revision and releases the new one atomically.
    /// The release and effective timestamps are both actual current UTC, separately recorded.
    /// </summary>
    [Fact]
    public async Task IT04_ReleaseSupersedesTheOldRevisionAndReleasesTheNewOne()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await database.SeedAsync();

        var eco = await CreateAsync(database, "Replace motherboard controller");
        await SubmitAsync(database, eco.Id);
        var approved = await ApproveAsync(database, eco.Id);

        await using (var db = database.CreateDbContext())
        {
            var released = await new ReleaseEngineeringChange(db)
                .ExecuteAsync(eco.Id, "Jordan Lee", confirmImmediateEffectivity: true);

            Assert.NotNull(released);
            Assert.Equal(EngineeringChangeStatus.Applied, released.Status);
        }

        await using var verify = database.CreateDbContext();

        var newRevision = await verify.BomRevisions.AsNoTracking()
            .SingleAsync(revision => revision.Id == approved.ApprovedRevisionId);
        var oldRevision = await verify.BomRevisions.AsNoTracking()
            .SingleAsync(revision => revision.Id == DemoSeed.RevBId);

        Assert.Equal(BomRevisionStatus.Released, newRevision.Status);

        Assert.NotNull(newRevision.EffectiveAtUtc);
        Assert.NotNull(newRevision.ReleasedAtUtc);
        Assert.Equal(newRevision.ReleasedAtUtc, newRevision.EffectiveAtUtc);

        Assert.Equal(BomRevisionStatus.Superseded, oldRevision.Status);
        Assert.Equal(new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc), oldRevision.EffectiveAtUtc);

        Assert.Equal(1, await verify.BomRevisions.CountAsync(
            revision => revision.ProductId == DemoSeed.Server100Id && revision.Status == BomRevisionStatus.Released));

        Assert.True(await verify.AuditEvents.AsNoTracking()
            .AnyAsync(audit => audit.Action == AuditActions.RevisionReleased && audit.EntityId == eco.Id));
    }

    [Fact]
    public async Task FutureRequestedEffectivityMustBeConfirmedAndConfirmedReleaseIsEffectiveNow()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await database.SeedAsync();

        var eco = await CreateAsync(database, "Future effectivity confirmation");
        await SubmitAsync(database, eco.Id);
        var approved = await ApproveAsync(database, eco.Id);

        await using (var db = database.CreateDbContext())
        {
            var error = await Assert.ThrowsAsync<DomainConflictException>(
                () => new ReleaseEngineeringChange(db).ExecuteAsync(
                    eco.Id, "Jordan Lee", confirmImmediateEffectivity: false));
            Assert.Equal(DomainErrorCodes.FutureEffectivityRequiresConfirmation, error.Code);
        }

        await using (var verify = database.CreateDbContext())
        {
            Assert.Equal(BomRevisionStatus.Released,
                (await verify.BomRevisions.AsNoTracking().SingleAsync(r => r.Id == DemoSeed.RevBId)).Status);
            Assert.Equal(BomRevisionStatus.Approved,
                (await verify.BomRevisions.AsNoTracking().SingleAsync(r => r.Id == approved.ApprovedRevisionId)).Status);
            Assert.Equal(EngineeringChangeStatus.Approved,
                (await verify.EngineeringChangeOrders.AsNoTracking().SingleAsync(e => e.Id == eco.Id)).Status);
        }

        var before = DateTime.UtcNow.AddSeconds(-1);
        await using (var db = database.CreateDbContext())
        {
            await new ReleaseEngineeringChange(db).ExecuteAsync(
                eco.Id, "Jordan Lee", confirmImmediateEffectivity: true);
        }
        var after = DateTime.UtcNow.AddSeconds(1);

        await using var final = database.CreateDbContext();
        var released = await final.BomRevisions.AsNoTracking()
            .SingleAsync(r => r.Id == approved.ApprovedRevisionId);
        Assert.InRange(released.ReleasedAtUtc!.Value, before, after);
        Assert.Equal(released.ReleasedAtUtc, released.EffectiveAtUtc);
        Assert.True(eco.RequestedEffectiveAtUtc > released.EffectiveAtUtc);

        var auditDetailsJson = await final.AuditEvents.AsNoTracking()
            .Where(a => a.Action == AuditActions.RevisionReleased && a.EntityId == eco.Id)
            .Select(a => a.Details)
            .SingleAsync();
        using var auditDetails = JsonDocument.Parse(auditDetailsJson);
        Assert.Equal(released.ReleasedAtUtc, auditDetails.RootElement.GetProperty("releasedAtUtc").GetDateTime());
        Assert.Equal(released.EffectiveAtUtc, auditDetails.RootElement.GetProperty("effectiveAtUtc").GetDateTime());
    }

    /// <summary>IT-05 — rejecting is terminal for the proposal and creates no BOM revision.</summary>
    [Fact]
    public async Task IT05_RejectingCreatesNoRevision()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await database.SeedAsync();

        var eco = await CreateAsync(database, "Replace motherboard controller");
        await SubmitAsync(database, eco.Id);

        await using (var db = database.CreateDbContext())
        {
            var rejected = await new RejectEngineeringChange(db)
                .ExecuteAsync(eco.Id, "Jordan Lee", "Alternative component already qualified.");

            Assert.NotNull(rejected);
            Assert.Equal(EngineeringChangeStatus.Rejected, rejected.Status);
            Assert.Equal("Alternative component already qualified.", rejected.ReviewerComment);
        }

        await using var verify = database.CreateDbContext();

        Assert.Equal(1, await verify.BomRevisions.CountAsync());
        Assert.False(await verify.BomRevisions.AsNoTracking()
            .AnyAsync(revision => revision.SourceEcoId == eco.Id));

        Assert.Equal(EngineeringChangeStatus.Rejected,
            (await verify.EngineeringChangeOrders.AsNoTracking().SingleAsync(order => order.Id == eco.Id)).Status);

        Assert.True(await verify.AuditEvents.AsNoTracking()
            .AnyAsync(audit => audit.Action == AuditActions.EcoRejected && audit.EntityId == eco.Id));
    }

    [Fact]
    public async Task ConcurrentApprovalLosesToTheCommittedRejectionWithoutCreatingARevision()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await database.SeedAsync();

        var eco = await CreateAsync(database, "Competing decision");
        await SubmitAsync(database, eco.Id);

        // Materialize the losing request's ECO before the winning decision commits. Its
        // original xmin must be used when it later attempts to approve.
        await using var staleRequest = database.CreateDbContext();
        await staleRequest.EngineeringChangeOrders.Include(order => order.Lines).SingleAsync(order => order.Id == eco.Id);

        await using (var winner = database.CreateDbContext())
        {
            var rejected = await new RejectEngineeringChange(winner)
                .ExecuteAsync(eco.Id, "Jordan Lee", "Rejected during concurrency test.");
            Assert.NotNull(rejected);
        }

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            new ApproveEngineeringChange(staleRequest).ExecuteAsync(eco.Id, "Jordan Lee", "Racing approval."));

        await using var verify = database.CreateDbContext();
        var persistedEco = await verify.EngineeringChangeOrders.AsNoTracking().SingleAsync(order => order.Id == eco.Id);
        Assert.Equal(EngineeringChangeStatus.Rejected, persistedEco.Status);
        Assert.Null(persistedEco.ApprovedRevisionId);
        Assert.False(await verify.BomRevisions.AsNoTracking().AnyAsync(revision => revision.SourceEcoId == eco.Id));
        Assert.False(await verify.AuditEvents.AsNoTracking()
            .AnyAsync(audit => audit.Action == AuditActions.EcoApproved && audit.EntityId == eco.Id));
        Assert.True(await verify.AuditEvents.AsNoTracking()
            .AnyAsync(audit => audit.Action == AuditActions.EcoRejected && audit.EntityId == eco.Id));
    }

    [Fact]
    public async Task StaleDraftEditCannotReplaceLinesAfterSubmissionEvenWhenScalarsAreUnchanged()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await database.SeedAsync();

        var eco = await CreateAsync(database, "Edit racing submission");
        await using var staleRequest = database.CreateDbContext();
        await staleRequest.EngineeringChangeOrders.Include(order => order.Lines).SingleAsync(order => order.Id == eco.Id);

        await SubmitAsync(database, eco.Id);

        var unchangedEdit = new EcoEdit(
            eco.Title,
            eco.ReasonForChange,
            eco.ImpactAssessment,
            eco.RequestedEffectiveAtUtc,
            "Alex Morgan",
            [new NewEcoLine(EngineeringChangeType.ReplaceComponent, DemoSeed.Mb020Id, DemoSeed.Mb021Id, null, "Controller spec update")]);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            new UpdateEngineeringChange(staleRequest).ExecuteAsync(eco.Id, unchangedEdit));

        await using var verify = database.CreateDbContext();
        var persisted = await verify.EngineeringChangeOrders.AsNoTracking()
            .Include(order => order.Lines)
            .SingleAsync(order => order.Id == eco.Id);
        Assert.Equal(EngineeringChangeStatus.Submitted, persisted.Status);
        Assert.Single(persisted.Lines);
        Assert.Equal("Controller spec update", persisted.Lines[0].Reason);
        Assert.False(await verify.AuditEvents.AsNoTracking()
            .AnyAsync(audit => audit.Action == AuditActions.EcoDraftUpdated && audit.EntityId == eco.Id));
    }

    /// <summary>
    /// IT-06 — two competing ECOs written against the same released revision cannot both
    /// release. The first wins; the second gets `stale-base-revision` and leaves no trace.
    /// </summary>
    [Fact]
    public async Task IT06_TwoCompetingEcosCannotBothRelease()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await database.SeedAsync();

        var first = await CreateAsync(database, "First competing change");
        var second = await CreateAsync(database, "Second competing change");

        // Both are valid proposals against revision B, and approval is not exclusive: each
        // creates its own Approved revision (C and D). Release is the exclusive action.
        await SubmitAsync(database, first.Id);
        await SubmitAsync(database, second.Id);
        var firstApproved = await ApproveAsync(database, first.Id);
        var secondApproved = await ApproveAsync(database, second.Id);

        await using (var db = database.CreateDbContext())
        {
            var winner = await new ReleaseEngineeringChange(db)
                .ExecuteAsync(first.Id, "Jordan Lee", confirmImmediateEffectivity: true);
            Assert.NotNull(winner);
        }

        await using (var db = database.CreateDbContext())
        {
            var ex = await Assert.ThrowsAsync<DomainConflictException>(
                () => new ReleaseEngineeringChange(db).ExecuteAsync(second.Id, "Jordan Lee", confirmImmediateEffectivity: true));

            Assert.Equal(DomainErrorCodes.StaleBaseRevision, ex.Code);
        }

        await using var verify = database.CreateDbContext();

        Assert.Equal(1, await verify.BomRevisions.CountAsync(
            revision => revision.ProductId == DemoSeed.Server100Id && revision.Status == BomRevisionStatus.Released));

        // The losing ECO is untouched: still approved, its revision still approved, not applied.
        Assert.Equal(EngineeringChangeStatus.Approved,
            (await verify.EngineeringChangeOrders.AsNoTracking().SingleAsync(order => order.Id == second.Id)).Status);
        Assert.Equal(BomRevisionStatus.Approved,
            (await verify.BomRevisions.AsNoTracking().SingleAsync(revision => revision.Id == secondApproved.ApprovedRevisionId)).Status);

        Assert.Equal(BomRevisionStatus.Released,
            (await verify.BomRevisions.AsNoTracking().SingleAsync(revision => revision.Id == firstApproved.ApprovedRevisionId)).Status);
    }

    /// <summary>
    /// IT-06's schema-level half — the `xmin` token actually refuses the losing write.
    ///
    /// This is the first test that exercises the concurrency token. Two contexts read the same
    /// product row (same `xmin`); the first transaction commits a product update, the second
    /// then cannot update it. That is exactly the guard approve and release rely on through
    /// `Product.MarkConfigurationChanged`. Without this running against PostgreSQL, no
    /// concurrency claim would be honest (ADR-004).
    /// </summary>
    [Fact]
    public async Task IT06_XminTokenRejectsTheLosingUpdateWhenTwoTransactionsReadTheSameProduct()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await database.SeedAsync();

        var eco = await CreateAsync(database, "Change guarded by xmin");
        await SubmitAsync(database, eco.Id);
        var approved = await ApproveAsync(database, eco.Id);

        await using var winner = database.CreateDbContext();
        await using var loser = database.CreateDbContext();

        await using var winnerTransaction = await winner.Database.BeginTransactionAsync();
        await using var loserTransaction = await loser.Database.BeginTransactionAsync();

        // Both read the product, and therefore the same original xmin, before either writes.
        var winnerProduct = await winner.Products.SingleAsync();
        var loserProduct = await loser.Products.SingleAsync();

        var currentReleased = await winner.BomRevisions.SingleAsync(revision => revision.Status == BomRevisionStatus.Released);
        var newRevision = await winner.BomRevisions.SingleAsync(revision => revision.Id == approved.ApprovedRevisionId);

        var now = DateTime.UtcNow;

        // Winner performs the release sequence and commits.
        winnerProduct.MarkConfigurationChanged(now);
        currentReleased.MarkSuperseded();
        await winner.SaveChangesAsync();

        newRevision.Release(now, now);
        await winner.SaveChangesAsync();

        await winnerTransaction.CommitAsync();

        // Loser still holds the stale xmin. Its first write must be refused, not silently applied.
        loserProduct.MarkConfigurationChanged(now);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => loser.SaveChangesAsync());

        await using var verify = database.CreateDbContext();

        Assert.Equal(BomRevisionStatus.Released,
            (await verify.BomRevisions.AsNoTracking().SingleAsync(revision => revision.Id == newRevision.Id)).Status);
        Assert.Equal(BomRevisionStatus.Superseded,
            (await verify.BomRevisions.AsNoTracking().SingleAsync(revision => revision.Id == currentReleased.Id)).Status);
    }

    /// <summary>
    /// IT-07, approval half — a failure after the revision insert leaves no revision, no
    /// approval link and no audit row. Forced with a trigger on the audit insert, so the
    /// revision insert really does happen first inside the transaction.
    /// </summary>
    [Fact]
    public async Task IT07_FailedApprovalRollsBackThePartialRevision()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await database.SeedAsync();

        var eco = await CreateAsync(database, "Approval that must roll back");
        await SubmitAsync(database, eco.Id);

        await InstallAuditFailureTriggerAsync(database, AuditActions.EcoApproved);

        await using (var db = database.CreateDbContext())
        {
            await Assert.ThrowsAsync<DbUpdateException>(
                () => new ApproveEngineeringChange(db).ExecuteAsync(eco.Id, "Jordan Lee", "will fail"));
        }

        await using var verify = database.CreateDbContext();

        Assert.Equal(1, await verify.BomRevisions.CountAsync());
        Assert.False(await verify.BomRevisions.AsNoTracking()
            .AnyAsync(revision => revision.SourceEcoId == eco.Id));

        var reloaded = await verify.EngineeringChangeOrders.AsNoTracking().SingleAsync(order => order.Id == eco.Id);
        Assert.Equal(EngineeringChangeStatus.Submitted, reloaded.Status);
        Assert.Null(reloaded.ApprovedRevisionId);
        Assert.False(await verify.AuditEvents.AsNoTracking()
            .AnyAsync(audit => audit.Action == AuditActions.EcoApproved));
    }

    /// <summary>
    /// IT-07, release half — the strongest rollback case. The supersede statement is saved
    /// first (by design), then the second save fails. The explicit transaction must undo the
    /// supersede too, or the product would be left with no current released revision.
    /// </summary>
    [Fact]
    public async Task IT07_FailedReleaseRollsBackTheAlreadySavedSupersede()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await database.SeedAsync();

        var eco = await CreateAsync(database, "Release that must roll back");
        await SubmitAsync(database, eco.Id);
        var approved = await ApproveAsync(database, eco.Id);

        int priorReleaseAuditCount;
        await using (var baseline = database.CreateDbContext())
        {
            priorReleaseAuditCount = await baseline.AuditEvents.AsNoTracking()
                .CountAsync(audit =>
                    audit.Action == AuditActions.RevisionReleased &&
                    audit.EntityType == "EngineeringChangeOrder" &&
                    audit.EntityId == eco.Id);
        }

        await InstallAuditFailureTriggerAsync(database, AuditActions.RevisionReleased);

        await using (var db = database.CreateDbContext())
        {
            await Assert.ThrowsAsync<DbUpdateException>(
                () => new ReleaseEngineeringChange(db).ExecuteAsync(eco.Id, "Jordan Lee", confirmImmediateEffectivity: true));
        }

        await using var verify = database.CreateDbContext();

        // The first (supersede) save was rolled back with the transaction.
        Assert.Equal(BomRevisionStatus.Released,
            (await verify.BomRevisions.AsNoTracking().SingleAsync(revision => revision.Id == DemoSeed.RevBId)).Status);
        Assert.Equal(BomRevisionStatus.Approved,
            (await verify.BomRevisions.AsNoTracking().SingleAsync(revision => revision.Id == approved.ApprovedRevisionId)).Status);

        Assert.Equal(EngineeringChangeStatus.Approved,
            (await verify.EngineeringChangeOrders.AsNoTracking().SingleAsync(order => order.Id == eco.Id)).Status);

        Assert.Equal(priorReleaseAuditCount, await verify.AuditEvents.AsNoTracking()
            .CountAsync(audit =>
                audit.Action == AuditActions.RevisionReleased &&
                audit.EntityType == "EngineeringChangeOrder" &&
                audit.EntityId == eco.Id));
    }

    /// <summary>
    /// A rejected ECO cannot be approved afterwards (spec §5.4). Driven through the service so
    /// the failure is the user-visible transition conflict, not just a domain-unit assertion.
    /// </summary>
    [Fact]
    public async Task ARejectedEcoCannotBeApprovedAfterwards()
    {
        await using var database = await fixture.CreateDatabaseAsync();
        await database.SeedAsync();

        var eco = await CreateAsync(database, "Rejected change");
        await SubmitAsync(database, eco.Id);

        await using (var db = database.CreateDbContext())
        {
            await new RejectEngineeringChange(db).ExecuteAsync(eco.Id, "Jordan Lee", "Not needed.");
        }

        await using (var db = database.CreateDbContext())
        {
            var ex = await Assert.ThrowsAsync<DomainConflictException>(
                () => new ApproveEngineeringChange(db).ExecuteAsync(eco.Id, "Jordan Lee", null));

            Assert.Equal(DomainErrorCodes.InvalidTransition, ex.Code);
        }
    }

    private static async Task InstallAuditFailureTriggerAsync(IsolatedDatabase database, string action)
    {
        await using var db = database.CreateDbContext();

        // `action` is one of the `AuditActions` constants supplied by this test, never user
        // input. It is concatenated rather than interpolated because a function body is a
        // quoted SQL string: a command parameter cannot appear inside it, and EF's analyzer
        // correctly refuses interpolated raw SQL.
        var functionSql =
            "CREATE OR REPLACE FUNCTION forge_fail_audit() RETURNS trigger AS $$ " +
            "BEGIN " +
            "IF NEW.action = '" + action + "' THEN " +
            "RAISE EXCEPTION 'forced audit failure for IT-07'; " +
            "END IF; " +
            "RETURN NEW; " +
            "END; " +
            "$$ LANGUAGE plpgsql;";

        await db.Database.ExecuteSqlRawAsync(functionSql);

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER forge_fail_audit
            BEFORE INSERT ON audit_events
            FOR EACH ROW EXECUTE FUNCTION forge_fail_audit();
            """);
    }
}
