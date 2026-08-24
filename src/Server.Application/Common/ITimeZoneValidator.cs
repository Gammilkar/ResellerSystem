namespace ResellerSystem.Server.Application.Common;

/// <summary>
/// Validates and normalizes time zone identifiers.
///
/// The system must not break on the difference between Windows time zone
/// IDs ("Pacific Standard Time") and IANA IDs ("America/Los_Angeles").
/// Implementations should accept either and normalize to IANA for storage
/// (IANA is what non-Windows clients/servers use, and .NET 6+ can convert
/// between the two cross-platform via ICU/TimeZoneInfo).
/// </summary>
public interface ITimeZoneValidator
{
    bool TryNormalize(string input, out string ianaId);
}
