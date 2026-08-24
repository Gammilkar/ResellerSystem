using System.Globalization;
using ResellerSystem.Server.Application.Common;

namespace ResellerSystem.Server.Infrastructure.Time;

/// <summary>
/// Validates ISO 4217 currency codes using the set of currencies known to
/// .NET's globalization data (RegionInfo). USD is always valid; the
/// abstraction exists so multi-currency support doesn't require touching
/// callers later.
/// </summary>
public sealed class CurrencyValidator : ICurrencyValidator
{
    private static readonly HashSet<string> KnownCurrencyCodes = CultureInfo
        .GetCultures(CultureTypes.SpecificCultures)
        .Select(culture =>
        {
            try { return new RegionInfo(culture.Name).ISOCurrencySymbol; }
            catch { return null; }
        })
        .Where(code => code is not null)
        .Select(code => code!)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public bool IsValid(string currencyCode)
    {
        if (string.IsNullOrWhiteSpace(currencyCode)) return false;
        var code = currencyCode.Trim();
        return code.Length == 3 && KnownCurrencyCodes.Contains(code);
    }
}
