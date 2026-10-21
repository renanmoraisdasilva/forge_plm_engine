using ForgePlm.Domain.BillOfMaterials;
using ForgePlm.Domain.Common;
using ForgePlm.Domain.EngineeringChanges;

namespace ForgePlm.Tests.EngineeringChanges;

public class EngineeringChangeOrderTests
{
    private static readonly Guid ProductId = Guid.NewGuid();

    private static (EngineeringChangeOrder Eco, BomRevision Base) DraftEco()
    {
        var baseRevision = TestData.Server100RevB(ProductId);
        return (TestData.ReplaceMotherboardEco(ProductId, baseRevision), baseRevision);
    }

    private static (EngineeringChangeOrder Eco, BomRevision Base) SubmittedEco()
    {
        var (eco, baseRevision) = DraftEco();
        eco.Submit(baseRevision, TestData.Now);
        return (eco, baseRevision);
    }

    // --- Creation -----------------------------------------------------------------

    [Fact]
    public void CreatingAnEcoCapturesTheBaseReleasedRevision()
    {
        var (eco, baseRevision) = DraftEco();

        Assert.Equal(baseRevision.Id, eco.BaseReleasedRevisionId);
        Assert.Equal(ProductId, eco.ProductId);
        Assert.Equal(EngineeringChangeStatus.Draft, eco.Status);
        Assert.Single(eco.Lines);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankEcoTitleIsRejected(string? title)
    {
        var baseRevision = TestData.Server100RevB(ProductId);

        var ex = Assert.Throws<DomainRuleViolationException>(
            () => EngineeringChangeOrder.Create("ECO-2026-0042", ProductId, baseRevision.Id, title, "reason", "impact", null, "Alex Morgan", TestData.Now));

        Assert.Equal(DomainErrorCodes.BlankTitle, ex.Code);
    }

    [Fact]
    public void ABlankActorIsRejected()
    {
        var baseRevision = TestData.Server100RevB(ProductId);

        var ex = Assert.Throws<DomainRuleViolationException>(
            () => EngineeringChangeOrder.Create("ECO-2026-0042", ProductId, baseRevision.Id, "title", "reason", "impact", null, "  ", TestData.Now));

        Assert.Equal(DomainErrorCodes.BlankActor, ex.Code);
    }

    [Fact]
    public void AnEcoWithoutAProductIsRejected()
    {
        var baseRevision = TestData.Server100RevB(ProductId);

        var ex = Assert.Throws<DomainRuleViolationException>(
            () => EngineeringChangeOrder.Create("ECO-2026-0042", Guid.Empty, baseRevision.Id, "title", "reason", "impact", null, "Alex Morgan", TestData.Now));

        Assert.Equal(DomainErrorCodes.ProductRequired, ex.Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankReasonForChangeIsRejected(string? reason)
    {
        var baseRevision = TestData.Server100RevB(ProductId);

        var ex = Assert.Throws<DomainRuleViolationException>(
            () => EngineeringChangeOrder.Create("ECO-2026-0042", ProductId, baseRevision.Id, "title", reason, "impact", null, "Alex Morgan", TestData.Now));

        Assert.Equal(DomainErrorCodes.ReasonForChangeRequired, ex.Code);
    }

    [Fact]
    public void AnOverlongEcoTextIsRejected()
    {
        var baseRevision = TestData.Server100RevB(ProductId);
        var tooLong = new string('x', EngineeringChangeOrder.MaxTextLength + 1);

        var ex = Assert.Throws<DomainRuleViolationException>(
            () => EngineeringChangeOrder.Create("ECO-2026-0042", ProductId, baseRevision.Id, "title", "reason", tooLong, null, "Alex Morgan", TestData.Now));

        Assert.Equal(DomainErrorCodes.TextTooLong, ex.Code);
    }

    // --- Change lines -------------------------------------------------------------

    [Fact]
    public void ReplacingAComponentWithItselfIsRejected()
    {
        var ex = Assert.Throws<DomainRuleViolationException>(
            () => EngineeringChangeLine.Replace(TestData.Motherboard020, TestData.Motherboard020, null));

        Assert.Equal(DomainErrorCodes.ReplacementMustDiffer, ex.Code);
    }

    [Fact]
    public void DT02_AQuantityChangeToZeroIsRejected()
    {
        var ex = Assert.Throws<DomainRuleViolationException>(
            () => EngineeringChangeLine.ChangeQuantity(TestData.Memory032, 0, null));

        Assert.Equal(DomainErrorCodes.NonPositiveQuantity, ex.Code);
    }

    [Fact]
    public void DT12_LinesCannotBeChangedOnceSubmitted()
    {
        var (eco, _) = SubmittedEco();

        var ex = Assert.Throws<DomainConflictException>(
            () => eco.ReplaceLines([EngineeringChangeLine.Replace(TestData.Motherboard020, TestData.Motherboard021, null)]));

        Assert.Equal(DomainErrorCodes.InvalidTransition, ex.Code);
    }

    [Fact]
    public void ADraftEcoCanBeEdited()
    {
        var (eco, _) = DraftEco();

        eco.UpdateDraft(
            "A better title",
            "A revised reason.",
            "A revised impact assessment.",
            TestData.Now.AddDays(2),
            [EngineeringChangeLine.ChangeQuantity(TestData.Memory032, 16, "Double the memory")]);

        Assert.Equal("A better title", eco.Title);
        Assert.Equal("A revised reason.", eco.ReasonForChange);
        Assert.Equal("A revised impact assessment.", eco.ImpactAssessment);
        Assert.Equal(TestData.Now.AddDays(2), eco.RequestedEffectiveAtUtc);
        Assert.Equal(16, eco.Lines.Single().NewQuantity);
    }

    [Fact]
    public void DT12_ASubmittedEcoCannotBeEdited()
    {
        var (eco, _) = SubmittedEco();

        var ex = Assert.Throws<DomainConflictException>(() => eco.UpdateDraft(
            "A better title",
            "reason",
            "impact",
            null,
            [EngineeringChangeLine.ChangeQuantity(TestData.Memory032, 16, null)]));

        Assert.Equal(DomainErrorCodes.InvalidTransition, ex.Code);
    }

    [Fact]
    public void ABadDraftEditDoesNotChangeTheEco()
    {
        var (eco, _) = DraftEco();
        var originalTitle = eco.Title;
        var originalLineCount = eco.Lines.Count;

        var ex = Assert.Throws<DomainRuleViolationException>(() => eco.UpdateDraft(
            "  ",
            "reason",
            "impact",
            null,
            [EngineeringChangeLine.ChangeQuantity(TestData.Memory032, 16, null)]));

        Assert.Equal(DomainErrorCodes.BlankTitle, ex.Code);
        Assert.Equal(originalTitle, eco.Title);
        Assert.Equal(originalLineCount, eco.Lines.Count);
    }

    [Fact]
    public void ABadDraftLineSetDoesNotChangeTheEco()
    {
        var (eco, _) = DraftEco();
        var originalTitle = eco.Title;
        var originalLineCount = eco.Lines.Count;

        var ex = Assert.Throws<DomainRuleViolationException>(() => eco.UpdateDraft(
            "A better title",
            "reason",
            "impact",
            null,
            [
                EngineeringChangeLine.Replace(TestData.Motherboard020, TestData.Motherboard021, null),
                EngineeringChangeLine.Replace(TestData.Motherboard020, TestData.Memory032, null),
            ]));

        Assert.Equal(DomainErrorCodes.DuplicateTargetComponent, ex.Code);
        Assert.Equal(originalTitle, eco.Title);
        Assert.Equal(originalLineCount, eco.Lines.Count);
    }

    [Fact]
    public void AnEcoMustHaveALineBeforeItCanBeSubmitted()
    {
        var baseRevision = TestData.Server100RevB(ProductId);
        var eco = EngineeringChangeOrder.Create("ECO-2026-0043", ProductId, baseRevision.Id, "title", "reason", "impact", null, "Alex Morgan", TestData.Now);

        var ex = Assert.Throws<DomainRuleViolationException>(() => eco.Submit(baseRevision, TestData.Now));

        Assert.Equal(DomainErrorCodes.EcoLinesRequired, ex.Code);
    }

    [Fact]
    public void SubmitRejectsABaseRevisionFromAnotherProduct()
    {
        var baseRevision = TestData.Server100RevB(ProductId);
        var otherProduct = Guid.NewGuid();
        var eco = EngineeringChangeOrder.Create("ECO-2026-0045", otherProduct, baseRevision.Id, "title", "reason", "impact", null, "Alex Morgan", TestData.Now);
        eco.ReplaceLines([EngineeringChangeLine.ChangeQuantity(TestData.Memory032, 16, null)]);

        var ex = Assert.Throws<DomainConflictException>(() => eco.Submit(baseRevision, TestData.Now));

        Assert.Equal(DomainErrorCodes.StaleBaseRevision, ex.Code);
    }

    [Fact]
    public void SubmitRejectsABaseRevisionThatIsNotReleased()
    {
        var approvedRevision = BomRevision.CreateApproved(
            ProductId,
            "C",
            [BomLine.Create(TestData.Memory032, 8, "Each")],
            "Alex Morgan",
            TestData.Now,
            sourceEcoId: null);
        var eco = EngineeringChangeOrder.Create("ECO-2026-0046", ProductId, approvedRevision.Id, "title", "reason", "impact", null, "Alex Morgan", TestData.Now);
        eco.ReplaceLines([EngineeringChangeLine.ChangeQuantity(TestData.Memory032, 16, null)]);

        var ex = Assert.Throws<DomainConflictException>(() => eco.Submit(approvedRevision, TestData.Now));

        Assert.Equal(DomainErrorCodes.StaleBaseRevision, ex.Code);
    }

    // --- Submission ---------------------------------------------------------------

    [Fact]
    public void DT04_SubmittingMovesTheEcoToSubmittedAndStampsTheTime()
    {
        var (eco, baseRevision) = DraftEco();

        eco.Submit(baseRevision, TestData.Now);

        Assert.Equal(EngineeringChangeStatus.Submitted, eco.Status);
        Assert.Equal(TestData.Now, eco.SubmittedAtUtc);
    }

    [Fact]
    public void DT04_ASubmittedEcoCannotBeSubmittedAgain()
    {
        var (eco, baseRevision) = SubmittedEco();

        var ex = Assert.Throws<DomainConflictException>(() => eco.Submit(baseRevision, TestData.Now));

        Assert.Equal(DomainErrorCodes.InvalidTransition, ex.Code);
    }

    [Fact]
    public void SubmissionRejectsATargetThatIsNotInTheBaseRevision()
    {
        var baseRevision = TestData.Server100RevB(ProductId);
        var eco = EngineeringChangeOrder.Create("ECO-2026-0044", ProductId, baseRevision.Id, "title", "reason", "impact", null, "Alex Morgan", TestData.Now);
        eco.ReplaceLines([EngineeringChangeLine.ChangeQuantity(Guid.NewGuid(), 4, null)]);

        var ex = Assert.Throws<DomainRuleViolationException>(() => eco.Submit(baseRevision, TestData.Now));

        Assert.Equal(DomainErrorCodes.TargetNotInBaseRevision, ex.Code);
    }

    [Fact]
    public void SubmissionRejectsTheWrongBaseRevision()
    {
        var baseRevision = TestData.Server100RevB(ProductId);
        var otherRevision = TestData.Server100RevB(ProductId);
        var eco = TestData.ReplaceMotherboardEco(ProductId, baseRevision);

        var ex = Assert.Throws<DomainConflictException>(() => eco.Submit(otherRevision, TestData.Now));

        Assert.Equal(DomainErrorCodes.StaleBaseRevision, ex.Code);
    }

    // --- Approval -----------------------------------------------------------------

    [Fact]
    public void DT05_ADraftEcoCannotBeApproved()
    {
        var (eco, _) = DraftEco();

        var ex = Assert.Throws<DomainConflictException>(() => eco.Approve(Guid.NewGuid(), "Jordan Lee", null, TestData.Now));

        Assert.Equal(DomainErrorCodes.InvalidTransition, ex.Code);
    }

    [Fact]
    public void DT07_AnApprovedEcoCannotBeApprovedAgain()
    {
        var (eco, _) = SubmittedEco();
        eco.Approve(Guid.NewGuid(), "Jordan Lee", null, TestData.Now);

        var ex = Assert.Throws<DomainConflictException>(() => eco.Approve(Guid.NewGuid(), "Jordan Lee", null, TestData.Now));

        Assert.Equal(DomainErrorCodes.InvalidTransition, ex.Code);
    }

    [Fact]
    public void DT08_AnEcoCannotBeApprovedIfItsBaseRevisionIsNoLongerCurrent()
    {
        var (eco, _) = SubmittedEco();
        var newerRevisionId = Guid.NewGuid();

        var ex = Assert.Throws<DomainConflictException>(() => eco.EnsureBaseRevisionIsStillCurrent(newerRevisionId));

        Assert.Equal(DomainErrorCodes.StaleBaseRevision, ex.Code);
    }

    [Fact]
    public void ApprovalSucceedsWhileTheBaseRevisionIsStillCurrent()
    {
        var (eco, baseRevision) = SubmittedEco();

        eco.EnsureBaseRevisionIsStillCurrent(baseRevision.Id);
    }

    [Fact]
    public void ApprovalRecordsTheReviewerTimeAndRevisionLink()
    {
        var (eco, _) = SubmittedEco();
        var approvedRevisionId = Guid.NewGuid();

        eco.Approve(approvedRevisionId, "Jordan Lee", "Validated on test bench TB-09.", TestData.Now);

        Assert.Equal(EngineeringChangeStatus.Approved, eco.Status);
        Assert.Equal(approvedRevisionId, eco.ApprovedRevisionId);
        Assert.Equal("Jordan Lee", eco.DecidedBy);
        Assert.Equal(TestData.Now, eco.DecidedAtUtc);
        Assert.Equal("Validated on test bench TB-09.", eco.ReviewerComment);
    }

    [Fact]
    public void ApprovalWithoutACommentLeavesTheCommentNull()
    {
        var (eco, _) = SubmittedEco();

        eco.Approve(Guid.NewGuid(), "Jordan Lee", "   ", TestData.Now);

        Assert.Null(eco.ReviewerComment);
    }

    // --- Rejection ----------------------------------------------------------------

    [Fact]
    public void DT05_ADraftEcoCannotBeRejected()
    {
        var (eco, _) = DraftEco();

        var ex = Assert.Throws<DomainConflictException>(() => eco.Reject("Jordan Lee", "because", TestData.Now));

        Assert.Equal(DomainErrorCodes.InvalidTransition, ex.Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DT06_RejectionRequiresANonBlankReason(string? reason)
    {
        var (eco, _) = SubmittedEco();

        var ex = Assert.Throws<DomainRuleViolationException>(() => eco.Reject("Jordan Lee", reason, TestData.Now));

        Assert.Equal(DomainErrorCodes.RejectionReasonRequired, ex.Code);
    }

    [Fact]
    public void RejectionRecordsTheReasonAndCannotBeApprovedAfterwards()
    {
        var (eco, _) = SubmittedEco();

        eco.Reject("Jordan Lee", "Alternative component already qualified.", TestData.Now);

        Assert.Equal(EngineeringChangeStatus.Rejected, eco.Status);
        Assert.Equal("Alternative component already qualified.", eco.ReviewerComment);

        var ex = Assert.Throws<DomainConflictException>(() => eco.Approve(Guid.NewGuid(), "Jordan Lee", null, TestData.Now));
        Assert.Equal(DomainErrorCodes.InvalidTransition, ex.Code);
    }

    // --- Building the approved revision -------------------------------------------

    [Fact]
    public void ApprovalRevisionAppliesTheReplacementAndPreservesEveryOtherLine()
    {
        var (eco, baseRevision) = SubmittedEco();

        var approved = eco.BuildApprovedRevision(baseRevision, "C", "Jordan Lee", TestData.Now);

        Assert.Equal("C", approved.RevisionCode);
        Assert.Equal(3, approved.Lines.Count);
        Assert.Equal(baseRevision.Lines.Count, approved.Lines.Count);

        // The motherboard line changed component; quantities of untouched lines carried over.
        Assert.DoesNotContain(approved.Lines, l => l.ComponentId == TestData.Motherboard020);
        Assert.Contains(approved.Lines, l => l.ComponentId == TestData.Motherboard021 && l.Quantity == 1);
        Assert.Contains(approved.Lines, l => l.ComponentId == TestData.Processor001 && l.Quantity == 2);
        Assert.Contains(approved.Lines, l => l.ComponentId == TestData.Memory032 && l.Quantity == 8);
    }

    [Fact]
    public void BuildingTheApprovedRevisionLeavesTheBaseRevisionUntouched()
    {
        var (eco, baseRevision) = SubmittedEco();
        var originalLines = baseRevision.Lines.Select(l => (l.ComponentId, l.Quantity)).ToList();

        eco.BuildApprovedRevision(baseRevision, "C", "Jordan Lee", TestData.Now);

        Assert.Equal(originalLines, baseRevision.Lines.Select(l => (l.ComponentId, l.Quantity)).ToList());
        Assert.Equal("B", baseRevision.RevisionCode);
    }

    [Fact]
    public void TheApprovedRevisionLinksBackToItsEco()
    {
        var (eco, baseRevision) = SubmittedEco();

        var approved = eco.BuildApprovedRevision(baseRevision, "C", "Jordan Lee", TestData.Now);

        Assert.Equal(eco.Id, approved.SourceEcoId);
    }

    [Fact]
    public void AQuantityChangeProducesTheNewQuantityAndKeepsTheComponent()
    {
        var baseRevision = TestData.Server100RevB(ProductId);
        var eco = EngineeringChangeOrder.Create("ECO-2026-0045", ProductId, baseRevision.Id, "Add memory", "More headroom.", "Impact", null, "Alex Morgan", TestData.Now);
        eco.ReplaceLines([EngineeringChangeLine.ChangeQuantity(TestData.Memory032, 16, null)]);
        eco.Submit(baseRevision, TestData.Now);

        var approved = eco.BuildApprovedRevision(baseRevision, "C", "Jordan Lee", TestData.Now);

        Assert.Contains(approved.Lines, l => l.ComponentId == TestData.Memory032 && l.Quantity == 16);
    }

    [Fact]
    public void BuildingARevisionFromTheWrongBaseIsRejected()
    {
        var (eco, baseRevision) = SubmittedEco();
        var otherRevision = TestData.Server100RevB(ProductId);

        var ex = Assert.Throws<DomainConflictException>(() => eco.BuildApprovedRevision(otherRevision, "C", "Jordan Lee", TestData.Now));

        Assert.Equal(DomainErrorCodes.StaleBaseRevision, ex.Code);
    }

    [Fact]
    public void AStaleReplacementThatDuplicatesAnExistingComponentIsRejected()
    {
        var baseRevision = TestData.Server100RevB(ProductId);
        var eco = EngineeringChangeOrder.Create("ECO-2026-0046", ProductId, baseRevision.Id, "Bad swap", "reason", "impact", null, "Alex Morgan", TestData.Now);

        // Replace the motherboard with the processor, which is already in the BOM.
        eco.ReplaceLines([EngineeringChangeLine.Replace(TestData.Motherboard020, TestData.Processor001, null)]);
        eco.Submit(baseRevision, TestData.Now);

        var ex = Assert.Throws<DomainRuleViolationException>(() => eco.BuildApprovedRevision(baseRevision, "C", "Jordan Lee", TestData.Now));

        Assert.Equal(DomainErrorCodes.DuplicateComponentInRevision, ex.Code);
    }

    // --- Applied ------------------------------------------------------------------

    [Fact]
    public void OnlyAnApprovedEcoCanBeApplied()
    {
        var (eco, _) = SubmittedEco();

        var ex = Assert.Throws<DomainConflictException>(() => eco.MarkApplied(TestData.Now));

        Assert.Equal(DomainErrorCodes.InvalidTransition, ex.Code);
    }

    [Fact]
    public void MarkingAppliedKeepsTheApprovedRevisionLink()
    {
        var (eco, _) = SubmittedEco();
        var approvedRevisionId = Guid.NewGuid();
        eco.Approve(approvedRevisionId, "Jordan Lee", null, TestData.Now);

        eco.MarkApplied(TestData.Now.AddMinutes(5));

        Assert.Equal(EngineeringChangeStatus.Applied, eco.Status);
        Assert.Equal(approvedRevisionId, eco.ApprovedRevisionId);
    }

    // --- Review fixes (code review, 2026-10-21) ----------------------------------

    /// <summary>
    /// Two changes to the same component must be rejected, not silently merged.
    ///
    /// Approval resolves each base line with `FirstOrDefault`, so the second line would be
    /// discarded — producing a revision that does not contain every change the reviewer
    /// approved. The UI would show two changes; the BOM would carry one.
    /// </summary>
    [Fact]
    public void TwoChangesToTheSameComponentAreRejected()
    {
        var baseRevision = TestData.Server100RevB(ProductId);
        var eco = EngineeringChangeOrder.Create("ECO-2026-0044", ProductId, baseRevision.Id, "title", "reason", "impact", null, "Alex Morgan", TestData.Now);

        var ex = Assert.Throws<DomainRuleViolationException>(() => eco.ReplaceLines(
        [
            EngineeringChangeLine.Replace(TestData.Motherboard020, TestData.Motherboard021, null),
            EngineeringChangeLine.ChangeQuantity(TestData.Motherboard020, 2, null),
        ]));

        Assert.Equal(DomainErrorCodes.DuplicateTargetComponent, ex.Code);
    }

    /// <summary>
    /// The immutability claim must hold against a caller who does not trust it. A released
    /// revision's lines could previously be mutated by casting the exposed list back to
    /// `List&lt;BomLine&gt;`, which would quietly rewrite history.
    /// </summary>
    [Fact]
    public void RevisionLinesCannotBeMutatedThroughACast()
    {
        var revision = TestData.Server100RevB(ProductId);
        var before = revision.Lines.Count;

        // `AsReadOnly()` wraps the list, so the cast fails outright: there is never an
        // object to mutate. Stronger than throwing on the mutation itself.
        var ex = Assert.Throws<InvalidCastException>(() =>
            ((List<BomLine>)revision.Lines).Add(BomLine.Create(TestData.Processor001, 1, "Each")));

        Assert.Contains("ReadOnlyCollection", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, revision.Lines.Count);
    }

    /// <summary>Same protection on the ECO's own change lines.</summary>
    [Fact]
    public void EcoLinesCannotBeMutatedThroughACast()
    {
        var (eco, _) = DraftEco();
        var before = eco.Lines.Count;

        Assert.Throws<InvalidCastException>(() =>
            ((List<EngineeringChangeLine>)eco.Lines).Clear());

        Assert.Equal(before, eco.Lines.Count);
    }

    /// <summary>
    /// Each failure must report its own code. These all previously returned
    /// `blank-part-number`, so a UI keying off codes could not tell a missing base revision
    /// from a missing replacement part.
    /// </summary>
    [Fact]
    public void MissingBaseRevisionReportsItsOwnCode()
    {
        var ex = Assert.Throws<DomainRuleViolationException>(() => EngineeringChangeOrder.Create(
            "ECO-2026-0045", ProductId, Guid.Empty, "title", "reason", "impact", null, "Alex Morgan", TestData.Now));

        Assert.Equal(DomainErrorCodes.BaseRevisionRequired, ex.Code);
    }

    [Fact]
    public void BlankEcoNumberReportsItsOwnCode()
    {
        var ex = Assert.Throws<DomainRuleViolationException>(() => EngineeringChangeOrder.Create(
            "   ", ProductId, Guid.NewGuid(), "title", "reason", "impact", null, "Alex Morgan", TestData.Now));

        Assert.Equal(DomainErrorCodes.BlankEcoNumber, ex.Code);
    }

    [Fact]
    public void MissingReplacementComponentReportsItsOwnCode()
    {
        var ex = Assert.Throws<DomainRuleViolationException>(() =>
            EngineeringChangeLine.Replace(TestData.Motherboard020, Guid.Empty, null));

        Assert.Equal(DomainErrorCodes.ReplacementComponentRequired, ex.Code);
    }

    [Fact]
    public void MissingChangeTargetReportsItsOwnCode()
    {
        var ex = Assert.Throws<DomainRuleViolationException>(() =>
            EngineeringChangeLine.ChangeQuantity(Guid.Empty, 2, null));

        Assert.Equal(DomainErrorCodes.TargetComponentRequired, ex.Code);
    }

    /// <summary>
    /// Spec §7 lists `revision-limit-reached` among the `409` conflict types. It used to be
    /// thrown as a rule violation, so the API would have mapped a conflict to `400`.
    /// </summary>
    [Fact]
    public void ExhaustingRevisionCodesIsAConflictNotAValidationFailure()
    {
        Assert.Throws<DomainConflictException>(() => RevisionCode.Next("Z"));
    }
}
