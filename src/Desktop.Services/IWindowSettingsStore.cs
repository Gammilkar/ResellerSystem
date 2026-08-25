using System.Text.Json;

namespace ResellerSystem.Desktop.Services;

public sealed record WindowSettings(double Width, double Height, bool IsMaximized);

/// <summary>Persists the main window's size/maximized state across
/// restarts — local to this machine, same pattern as ITableSettingsStore.</summary>
public interface IWindowSettingsStore
{
    WindowSettings? Load();
    void Save(WindowSettings settings);
}

public sealed class WindowSettingsStore : IWindowSettingsStore
{
    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ResellerSystem Client", "window-settings.json");

    public WindowSettings? Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return null;
            return JsonSerializer.Deserialize<WindowSettings>(File.ReadAllText(FilePath));
        }
        catch
        {
            return null; // corrupt/unreadable — fall back to the default window size rather than crash
        }
    }

    public void Save(WindowSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(settings));
        }
        catch
        {
            // Best effort — losing a window-size preference is not worth crashing over.
        }
    }
}
