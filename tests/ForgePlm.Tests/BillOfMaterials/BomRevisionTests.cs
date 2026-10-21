using ForgePlm.Domain.BillOfMaterials;
using ForgePlm.Domain.Common;

namespace ForgePlm.Tests.BillOfMaterials;

public class BomRevisionTests
{
    private static readonly Guid ProductId = Guid.NewGuid();

    private static BomLine Line(Guid componentId, int quantity = 1) => BomLine.Create(componentId, quantity, "Each");

    [Fact]
    public void DT02_ANonPositiveLineQuantityIsRejected()
    {
        var ex = Assert.Throws<DomainRuleViolationException>(() => Line(TestData.Motherboard020, 0));
        Assert.Equal(DomainErrorCodes.NonPositiveQuantity, ex.Code);
    }

    [Fact]
    public void AnOverlongUnitIsRejectedByTheDomain()
    {
        var ex = Assert.Throws<DomainRuleViolationException>(() => BomLine.Create(
            TestData.Motherboard020,
            1,
            new string('x', BomLine.MaxUnitLength + 1)));

        Assert.Equal(DomainErrorCodes.TextTooLong, ex.Code);
    }

    [Fact]
    public void DT10_AComponentCanAppearOnlyOncePerRevision()
    {
        var ex = Assert.Throws<DomainRuleViolationException>(
            () => BomRevision.CreateApproved(
                ProductId,
                "C",
                [Line(TestData.Motherboard020), Line(TestData.Motherboard020, 2)],
                "Alex Morgan",
                TestData.Now,
                sourceEcoId: Guid.NewGuid()));

        Assert.Equal(DomainErrorCodes.DuplicateComponentInRevision, ex.Code);
    }

    [Fact]
    public void ARevisionMustHaveAtLeastOneLine()
    {
        var ex = Assert.Throws<DomainRuleViolationException>(
            () => BomRevision.CreateApproved(ProductId, "C", [], "Alex Morgan", TestData.Now, sourceEcoId: Guid.NewGuid()));

        Assert.Equal(DomainErrorCodes.BomLinesRequired, ex.Code);
    }

    [Fact]
    public void AnInvalidRevisionCodeIsRejected()
    {
        var ex = Assert.Throws<DomainRuleViolationException>(
            () => BomRevision.CreateApproved(ProductId, "AA", [Line(TestData.Motherboard020)], "Alex Morgan", TestData.Now, sourceEcoId: Guid.NewGuid()));

        Assert.Equal(DomainErrorCodes.InvalidRevisionCode, ex.Code);
    }

    [Fact]
    public void AnApprovedRevisionStartsApprovedWithoutAnEffectiveDate()
    {
        var revision = BomRevision.CreateApproved(
            ProductId, "C", [Line(TestData.Motherboard020)], "Alex Morgan", TestData.Now, sourceEcoId: Guid.NewGuid());

        Assert.Equal(BomRevisionStatus.Approved, revision.Status);
        Assert.Null(revision.EffectiveAtUtc);
        Assert.NotNull(revision.SourceEcoId);
    }

    [Fact]
    public void AnInitialReleasedBaselineStartsAtRevisionAWithReleaseAndEffectivityTimes()
    {
        var revision = BomRevision.CreateReleasedBaseline(
            ProductId,
            "A",
            [Line(TestData.Motherboard020)],
            "Alex Morgan",
            TestData.Now,
            TestData.Now);

        Assert.Equal(BomRevisionStatus.Released, revision.Status);
        Assert.Equal("A", revision.RevisionCode);
        Assert.Null(revision.SourceEcoId);
        Assert.Equal(TestData.Now, revision.ReleasedAtUtc);
        Assert.Equal(TestData.Now, revision.EffectiveAtUtc);
        Assert.Single(revision.Lines);
    }

    [Fact]
    public void DT03_AnApprovedRevisionCannotBeEdited()
    {
        var revision = BomRevision.CreateApproved(
            ProductId, "C", [Line(TestData.Motherboard020)], "Alex Morgan", TestData.Now, sourceEcoId: Guid.NewGuid());

        var ex = Assert.Throws<DomainConflictException>(revision.EnsureLinesAreEditable);

        Assert.Equal(DomainErrorCodes.InvalidTransition, ex.Code);
    }

    [Fact]
    public void DT03_AReleasedRevisionCannotBeEditedNorReleasedTwice()
    {
        var approved = BomRevision.CreateApproved(
            ProductId, "C", [Line(TestData.Motherboard020)], "Alex Morgan", TestData.Now, sourceEcoId: Guid.NewGuid());

        approved.Release(TestData.Now, TestData.Now);

        Assert.Throws<DomainConflictException>(approved.EnsureLinesAreEditable);

        var ex = Assert.Throws<DomainConflictException>(() => approved.Release(TestData.Now, TestData.Now.AddDays(1)));
        Assert.Equal(DomainErrorCodes.InvalidTransition, ex.Code);
    }

    [Fact]
    public void ReleaseSetsTheEffectiveTimestampAndMarksTheRevisionReleased()
    {
        var approved = BomRevision.CreateApproved(
            ProductId, "C", [Line(TestData.Motherboard020)], "Alex Morgan", TestData.Now, sourceEcoId: Guid.NewGuid());

        approved.Release(TestData.Now, TestData.Now.AddDays(11));

        Assert.Equal(BomRevisionStatus.Released, approved.Status);
        Assert.Equal(TestData.Now.AddDays(11), approved.EffectiveAtUtc);
        Assert.Equal(TestData.Now, approved.ReleasedAtUtc);
    }

    [Fact]
    public void DT13_AFutureEffectiveDateIsAccepted()
    {
        var approved = BomRevision.CreateApproved(
            ProductId, "C", [Line(TestData.Motherboard020)], "Alex Morgan", TestData.Now, sourceEcoId: Guid.NewGuid());

        approved.Release(TestData.Now, TestData.Now.AddYears(1));

        Assert.True(approved.EffectiveAtUtc > TestData.Now);
    }

    [Fact]
    public void OnlyAReleasedRevisionCanBeSuperseded()
    {
        var approved = BomRevision.CreateApproved(
            ProductId, "C", [Line(TestData.Motherboard020)], "Alex Morgan", TestData.Now, sourceEcoId: Guid.NewGuid());

        var ex = Assert.Throws<DomainConflictException>(approved.MarkSuperseded);
        Assert.Equal(DomainErrorCodes.InvalidTransition, ex.Code);
    }

    [Fact]
    public void SupersedingChangesTheStatusAndNothingElse()
    {
        var baseline = TestData.Server100RevB(ProductId);

        baseline.MarkSuperseded();

        Assert.Equal(BomRevisionStatus.Superseded, baseline.Status);
        Assert.Equal(3, baseline.Lines.Count);
        Assert.Equal(TestData.Now.AddDays(-30), baseline.EffectiveAtUtc);
    }

    [Fact]
    public void ABaselineCreatedByTheSeedIsReleasedWithNoSourceEco()
    {
        var baseline = TestData.Server100RevB(ProductId);

        Assert.Equal(BomRevisionStatus.Released, baseline.Status);
        Assert.Equal("B", baseline.RevisionCode);
        Assert.Null(baseline.SourceEcoId);
        Assert.NotNull(baseline.EffectiveAtUtc);
    }
}
