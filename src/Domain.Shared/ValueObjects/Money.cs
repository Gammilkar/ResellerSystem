namespace ResellerSystem.Domain.Shared.ValueObjects;

/// <summary>
/// Value object for monetary amounts. Always backed by decimal — never double/float.
/// Currency is carried alongside the amount so values are never ambiguous once
/// multi-currency support is introduced.
/// </summary>
public readonly record struct Money(decimal Amount, string Currency)
{
    public static Money Zero(string currency) => new(0m, currency);

    public static Money Usd(decimal amount) => new(amount, "USD");

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount - other.Amount, Currency);
    }

    private void EnsureSameCurrency(Money other)
    {
        if (!string.Equals(Currency, other.Currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Cannot combine amounts in different currencies: {Currency} vs {other.Currency}.");
        }
    }

    public override string ToString() => $"{Amount:0.00} {Currency}";
}
