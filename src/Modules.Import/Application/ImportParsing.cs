using System.Globalization;
using System.Text.RegularExpressions;

namespace ResellerSystem.Modules.Import.Application;

/// <summary>
/// Shared value parsing for staged import rows. Deliberately NOT tied to
/// one fixed culture — real-world exports mix "1,234.56" (US),
/// "1.234,56" (EU/RU), and plain "67,85" (comma as the only decimal
/// separator, common in RU spreadsheets like a personal reseller
/// tracker) in the same column, so decimal parsing sniffs the actual
/// punctuation per value instead of assuming one culture for the whole
/// file — see TryParseDecimal.
/// </summary>
internal static partial class ImportParsing
{
    public static bool TryParseDecimal(string? text, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var t = text.Trim().Replace("$", "").Replace("€", "").Replace("₽", "").Replace(" ", "").Trim();
        if (t.Length == 0) return false;

        var lastComma = t.LastIndexOf(',');
        var lastDot = t.LastIndexOf('.');

        string normalized;
        if (lastComma >= 0 && lastDot >= 0)
        {
            // Both present — whichever comes last is the decimal separator,
            // the other is a thousands separator (e.g. "1,234.56" or "1.234,56").
            normalized = lastDot > lastComma
                ? t.Replace(",", "")
                : t.Replace(".", "").Replace(',', '.');
        }
        else if (lastComma >= 0)
        {
            // Only a comma — decimal separator unless it clearly groups
            // thousands (exactly 3 digits after it AND more than one group,
            // e.g. "12,345,678"); "67,85" and "1,797.31"-without-the-dot
            // ("1,797") both come out as decimals, matching how a European/
            // RU-locale spreadsheet actually enters money.
            var afterLastComma = t[(lastComma + 1)..];
            var commaCount = t.Count(c => c == ',');
            normalized = commaCount > 1 || afterLastComma.Length == 3
                ? t.Replace(",", "")
                : t.Replace(',', '.');
        }
        else
        {
            normalized = t;
        }

        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    [GeneratedRegex(@"^(\d{1,2})[./-](\d{1,2})[./-](\d{4})")]
    private static partial Regex DayMonthYearRegex();

    public static bool TryParseDate(string? text, out DateOnly value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var t = text.Trim();

        // Explicit day-first check FIRST: a value like "16.06.2026 0:00:00"
        // (this exact shape comes straight out of Excel's default RU date
        // formatting) must never be handed to a month-first parser — day=16
        // isn't a valid month, so it would simply fail there, but a
        // similar-looking "05.06.2026" (day=5, month=6) would silently
        // parse as month=5/day=6 under an M/d culture. Only trust that
        // ambiguous case once day-first has already had first refusal.
        var dmy = DayMonthYearRegex().Match(t);
        if (dmy.Success)
        {
            var day = int.Parse(dmy.Groups[1].Value);
            var month = int.Parse(dmy.Groups[2].Value);
            var year = int.Parse(dmy.Groups[3].Value);
            if (month is >= 1 and <= 12 && day is >= 1 and <= 31)
            {
                try { value = new DateOnly(year, month, day); return true; }
                catch (ArgumentOutOfRangeException) { /* e.g. Feb 30 — fall through */ }
            }
        }

        if (DateOnly.TryParse(t, CultureInfo.InvariantCulture, DateTimeStyles.None, out value)) return true;
        return DateOnly.TryParse(t, out value); // best-effort fallback for anything else
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
