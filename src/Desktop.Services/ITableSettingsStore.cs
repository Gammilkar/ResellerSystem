using System.Text.Json;

namespace ResellerSystem.Desktop.Services;

public sealed record TableSettings(
    double FontSize,
    Dictionary<string, bool> ColumnVisibility,
    double HeaderFontSize = 0,
    Dictionary<string, double>? ColumnWidths = null);

/// <summary>Per-grid display preferences (font size, which columns are
/// shown) — local to this machine/user, not synced to the server. Plain
/// JSON, nothing sensitive here (unlike TrustedDeviceStore).</summary>
public interface ITableSettingsStore
{
    TableSettings? Load(string tableKey);
    void Save(string tableKey, TableSettings settings);
}

public sealed class TableSettingsStore : ITableSettingsStore
{
    private static string DirPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ResellerSystem Client", "table-settings");

    private static string FilePath(string tableKey) => Path.Combine(DirPath, $"{tableKey}.json");

    public TableSettings? Load(string tableKey)
    {
        try
        {
            var path = FilePath(tableKey);
            if (!File.Exists(path)) return null;
            return JsonSerializer.Deserialize<TableSettings>(File.ReadAllText(path));
        }
        catch
        {
            return null; // corrupt/unreadable — fall back to defaults rather than crash
        }
    }

    public void Save(string tableKey, TableSettings settings)
    {
        try
        {
            Directory.CreateDirectory(DirPath);
            File.WriteAllText(FilePath(tableKey), JsonSerializer.Serialize(settings));
        }
        catch
        {
            // Best effort — losing a display preference is not worth crashing over.
        }
    }
}
