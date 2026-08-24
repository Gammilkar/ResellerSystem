using ResellerSystem.Server.Application.Common;

namespace ResellerSystem.Server.Infrastructure.Time;

/// <summary>
/// Accepts both IANA ("America/Los_Angeles") and Windows ("Pacific Standard
/// Time") identifiers and normalizes to IANA for storage, using the
/// cross-platform conversion built into .NET 6+ (TimeZoneInfo on Linux/macOS
/// understands Windows IDs via ICU, and vice versa). This avoids the classic
/// bug where a Windows-authored value can't be parsed on the server or a
/// non-Windows client, or vice versa.
/// </summary>
public sealed class TimeZoneValidator : ITimeZoneValidator
{
    public bool TryNormalize(string input, out string ianaId)
    {
        ianaId = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var trimmed = input.Trim();

        // Already a valid identifier on this platform (IANA on Linux/macOS,
        // and — since .NET 6 — also resolvable from an IANA id on Windows).
        if (TimeZoneInfo.TryFindSystemTimeZoneById(trimmed, out var tz))
        {
            // If what we resolved is a Windows-style id, convert it to IANA
            // so the stored value is portable across server/client platforms.
            if (TimeZoneInfo.TryConvertWindowsIdToIanaId(tz.Id, out var convertedIana))
            {
                ianaId = convertedIana;
            }
            else
            {
                ianaId = tz.Id;
            }
            return true;
        }

        // Input might be an IANA id that TryFindSystemTimeZoneById doesn't
        // resolve directly to a Windows-style Id property but that TryConvert
        // still recognizes as a valid mapping source.
        if (TimeZoneInfo.TryConvertIanaIdToWindowsId(trimmed, out _))
        {
            ianaId = trimmed;
            return true;
        }

        return false;
    }
}
