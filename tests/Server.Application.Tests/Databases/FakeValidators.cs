using ResellerSystem.Server.Application.Common;

namespace ResellerSystem.Server.Application.Tests.Databases;

/// <summary>Minimal, deterministic fakes so provisioning-flow tests don't
/// depend on the real ICU-backed TimeZoneInfo implementation in Server.Infrastructure.</summary>
internal sealed class FakeTimeZoneValidator : ITimeZoneValidator
{
    public bool TryNormalize(string input, out string ianaId)
    {
        if (input is "America/Los_Angeles" or "UTC" or "America/New_York")
        {
            ianaId = input;
            return true;
        }

        ianaId = string.Empty;
        return false;
    }
}

internal sealed class FakeCurrencyValidator : ICurrencyValidator
{
    public bool IsValid(string currencyCode) => currencyCode is "USD" or "EUR";
}
