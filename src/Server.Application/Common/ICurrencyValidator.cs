namespace ResellerSystem.Server.Application.Common;

/// <summary>Validates ISO 4217 currency codes. Stage 1 only needs USD, but the
/// abstraction avoids hardcoding a single-currency assumption in validators.</summary>
public interface ICurrencyValidator
{
    bool IsValid(string currencyCode);
}
