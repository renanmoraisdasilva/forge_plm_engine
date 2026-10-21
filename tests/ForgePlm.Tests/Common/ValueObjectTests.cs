using ForgePlm.Domain.Common;

namespace ForgePlm.Tests.Common;

public class PartNumberTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DT01_BlankPartNumbersAreRejected(string? raw)
    {
        var ex = Assert.Throws<DomainRuleViolationException>(() => PartNumber.From(raw));
        Assert.Equal(DomainErrorCodes.BlankPartNumber, ex.Code);
    }

    [Fact]
    public void PartNumberIsNormalisedToUpperCaseAndTrimmed()
    {
        Assert.Equal("MB-020", PartNumber.From("  mb-020  ").Value);
    }

    [Fact]
    public void PartNumbersCompareCaseInsensitivelyAfterNormalisation()
    {
        Assert.Equal(PartNumber.From("mb-020"), PartNumber.From("MB-020"));
    }

    [Fact]
    public void OverlongPartNumberIsRejected()
    {
        var ex = Assert.Throws<DomainRuleViolationException>(() => PartNumber.From(new string('X', 51)));
        Assert.Equal(DomainErrorCodes.PartNumberTooLong, ex.Code);
    }
}

public class QuantityTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void DT02_NonPositiveQuantitiesAreRejected(int value)
    {
        var ex = Assert.Throws<DomainRuleViolationException>(() => Quantity.From(value));
        Assert.Equal(DomainErrorCodes.NonPositiveQuantity, ex.Code);
    }

    [Fact]
    public void PositiveQuantityIsAccepted()
    {
        Assert.Equal(8, Quantity.From(8).Value);
    }
}

public class RevisionCodeTests
{
    [Theory]
    [InlineData("A", "B")]
    [InlineData("B", "C")]
    [InlineData("Y", "Z")]
    public void DT11_RevisionCodesAdvanceSequentially(string current, string expected)
    {
        Assert.Equal(expected, RevisionCode.Next(current));
    }

    [Fact]
    public void DT11_RevisionCodeFailsPastZInsteadOfRollingOver()
    {
        var ex = Assert.Throws<DomainRuleViolationException>(() => RevisionCode.Next("Z"));
        Assert.Equal(DomainErrorCodes.RevisionLimitReached, ex.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("AA")]
    [InlineData("1")]
    [InlineData("a!")]
    public void InvalidRevisionCodesAreRejected(string code)
    {
        var ex = Assert.Throws<DomainRuleViolationException>(() => RevisionCode.EnsureValid(code));
        Assert.Equal(DomainErrorCodes.InvalidRevisionCode, ex.Code);
    }
}
