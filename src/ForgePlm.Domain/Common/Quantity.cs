namespace ForgePlm.Domain.Common;

/// <summary>
/// A positive quantity. Stored on the entity as a plain int for the same reason as
/// <see cref="PartNumber"/>: this type exists to make the invalid value unrepresentable
/// at the domain boundary, without asking EF Core to convert anything.
/// </summary>
public readonly record struct Quantity
{
    public int Value { get; }

    private Quantity(int value) => Value = value;

    public static Quantity From(int value)
    {
        if (value <= 0)
        {
            throw new DomainRuleViolationException(
                DomainErrorCodes.NonPositiveQuantity,
                $"Quantity must be greater than zero (received {value}).");
        }

        return new Quantity(value);
    }

    public override string ToString() => Value.ToString();
}
