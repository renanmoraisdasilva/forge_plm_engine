using ForgePlm.Domain.Audit;
using ForgePlm.Domain.Common;

namespace ForgePlm.Tests.Audit;

public class AuditEventTests
{
    [Fact]
    public void AnAuditEventRecordsActorActionEntityAndTime()
    {
        var entityId = Guid.NewGuid();

        var audit = AuditEvent.Record(
            "Jordan Lee", AuditActions.EcoApproved, "EngineeringChangeOrder", entityId, """{"ecoNumber":"ECO-2026-0042"}""", TestData.Now);

        Assert.Equal("Jordan Lee", audit.Actor);
        Assert.Equal("EcoApproved", audit.Action);
        Assert.Equal("EngineeringChangeOrder", audit.EntityType);
        Assert.Equal(entityId, audit.EntityId);
        Assert.Equal(TestData.Now, audit.OccurredAtUtc);
        Assert.Contains("ECO-2026-0042", audit.Details);
    }

    [Fact]
    public void MissingDetailsDefaultToAnEmptyObject()
    {
        var audit = AuditEvent.Record("Alex Morgan", AuditActions.ProductCreated, "Product", Guid.NewGuid(), null, TestData.Now);

        Assert.Equal("{}", audit.Details);
    }

    [Fact]
    public void AnAuditEventWithoutAnActorIsRejected()
    {
        var ex = Assert.Throws<DomainRuleViolationException>(
            () => AuditEvent.Record(" ", AuditActions.EcoCreated, "EngineeringChangeOrder", Guid.NewGuid(), null, TestData.Now));

        Assert.Equal(DomainErrorCodes.BlankActor, ex.Code);
    }

    [Fact]
    public void EveryActionTheDemoDependsOnHasAConstant()
    {
        Assert.Equal(
            ["ProductCreated", "ComponentCreated", "EcoCreated", "EcoSubmitted", "EcoApproved", "EcoRejected", "RevisionReleased"],
            [
                AuditActions.ProductCreated,
                AuditActions.ComponentCreated,
                AuditActions.EcoCreated,
                AuditActions.EcoSubmitted,
                AuditActions.EcoApproved,
                AuditActions.EcoRejected,
                AuditActions.RevisionReleased,
            ]);
    }
}
