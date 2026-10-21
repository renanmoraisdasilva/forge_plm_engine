using ForgePlm.Domain.Common;
using ForgePlm.Domain.Products;

namespace ForgePlm.Tests.Products;

public class ProductTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DT01_BlankNameIsRejected(string? name)
    {
        var ex = Assert.Throws<DomainRuleViolationException>(
            () => Product.Create("SERVER-100", name, "Server", ProductLifecycleStatus.Active, TestData.Now));

        Assert.Equal(DomainErrorCodes.BlankName, ex.Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void DT01_DT14_BlankOrWhitespacePartNumberIsRejected(string? partNumber)
    {
        var ex = Assert.Throws<DomainRuleViolationException>(
            () => Product.Create(partNumber, "Compute Server", "Server", ProductLifecycleStatus.Active, TestData.Now));

        Assert.Equal(DomainErrorCodes.BlankPartNumber, ex.Code);
    }

    [Fact]
    public void PartNumberIsStoredNormalised()
    {
        var product = Product.Create(" server-100 ", "Compute Server", "Server", ProductLifecycleStatus.Active, TestData.Now);

        Assert.Equal("SERVER-100", product.PartNumber);
    }

    [Fact]
    public void CategoryIsTrimmedAndDefaultsToEmptyWhenOmitted()
    {
        var withCategory = Product.Create("SERVER-100", "Compute Server", "  Server  ", ProductLifecycleStatus.Active, TestData.Now);
        var withoutCategory = Product.Create("SERVER-200", "Storage Server", null, ProductLifecycleStatus.Active, TestData.Now);

        Assert.Equal("Server", withCategory.Category);
        Assert.Equal(string.Empty, withoutCategory.Category);
    }

    [Fact]
    public void AnOverlongCategoryIsRejectedByTheDomain()
    {
        var ex = Assert.Throws<DomainRuleViolationException>(() => Product.Create(
            "SERVER-100",
            "Compute Server",
            new string('x', Product.MaxCategoryLength + 1),
            ProductLifecycleStatus.Active,
            TestData.Now));

        Assert.Equal(DomainErrorCodes.TextTooLong, ex.Code);
    }

    [Fact]
    public void RetiredProductCannotReceiveANewlyReleasedRevision()
    {
        var product = Product.Create("SERVER-100", "Compute Server", "Server", ProductLifecycleStatus.Retired, TestData.Now);

        var ex = Assert.Throws<DomainConflictException>(product.EnsureCanReceiveNewlyReleasedRevision);

        Assert.Equal(DomainErrorCodes.ProductRetired, ex.Code);
    }

    [Fact]
    public void ActiveProductCanReceiveANewlyReleasedRevision()
    {
        var product = Product.Create("SERVER-100", "Compute Server", "Server", ProductLifecycleStatus.Active, TestData.Now);

        product.EnsureCanReceiveNewlyReleasedRevision();
    }

    [Fact]
    public void OnlyAnActiveProductCanReceiveAnEco()
    {
        var draft = Product.Create("SERVER-100", "Compute Server", "Server", ProductLifecycleStatus.Draft, TestData.Now);

        var ex = Assert.Throws<DomainConflictException>(draft.EnsureIsActiveForChangeProposal);

        Assert.Equal(DomainErrorCodes.InvalidTransition, ex.Code);
    }
}
