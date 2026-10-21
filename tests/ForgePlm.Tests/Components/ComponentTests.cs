using ForgePlm.Domain.Common;
using ForgePlm.Domain.Components;

namespace ForgePlm.Tests.Components;

public class ComponentTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DT01_DT14_BlankNameIsRejected(string? name)
    {
        var ex = Assert.Throws<DomainRuleViolationException>(
            () => Component.Create("MB-020", name, "Motherboard", ComponentLifecycleStatus.Active, TestData.Now));

        Assert.Equal(DomainErrorCodes.BlankName, ex.Code);
    }

    [Fact]
    public void PartNumberIsStoredNormalised()
    {
        var component = Component.Create(" mb-020 ", "Server Motherboard", "Motherboard", ComponentLifecycleStatus.Active, TestData.Now);

        Assert.Equal("MB-020", component.PartNumber);
        Assert.True(component.IsActive);
    }

    [Theory]
    [InlineData(ComponentLifecycleStatus.Retired)]
    [InlineData(ComponentLifecycleStatus.Draft)]
    public void DT09_ANonActiveComponentCannotBeUsedInANewBom(ComponentLifecycleStatus status)
    {
        var component = Component.Create("MB-020", "Server Motherboard", "Motherboard", status, TestData.Now);

        var ex = Assert.Throws<DomainRuleViolationException>(component.EnsureCanBeUsedInNewBom);

        Assert.Equal(DomainErrorCodes.ComponentNotActive, ex.Code);
    }

    [Fact]
    public void AnActiveComponentCanBeUsedInANewBom()
    {
        var component = Component.Create("MB-020", "Server Motherboard", "Motherboard", ComponentLifecycleStatus.Active, TestData.Now);

        component.EnsureCanBeUsedInNewBom();
    }
}
