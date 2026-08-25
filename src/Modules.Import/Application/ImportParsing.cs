using System.Globalization;

namespace ResellerSystem.Modules.Import.Application;

/// <summary>Shared value parsing for staged import rows — always
/// culture-invariant (spreadsheets come from all kinds of locales;
/// guessing based on the server's OS locale would be wrong more often
/// than not, see Product Specification section 86).</summary>
internal static class ImportParsing
{
    public static bool TryParseDecimal(string? text, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var cleaned = text.Trim().Replace("$", "").Replace(",", "");
        return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    public static bool TryParseDate(string? text, out DateOnly value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        if (DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out value)) return true;
        return DateOnly.TryParse(text, out value); // best-effort fallback for odd source formats
    }

    public static bool ParseBool(string? text, bool defaultValue = false)
    {
        if (string.IsNullOrWhiteSpace(text)) return defaultValue;
        var t = text.Trim().ToLowerInvariant();
        return t is "yes" or "true" or "1" or "y" or "да" or "д";
    }

    public static string? GetOrNull(this IReadOnlyDictionary<string, string> mapped, string key) =>
        mapped.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;
}
