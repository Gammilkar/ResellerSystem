namespace ResellerSystem.Desktop.Services;

/// <summary>Abstraction over the native file-open dialog so ViewModels
/// don't need to reference Avalonia types directly — see Desktop.App's
/// FilePickerService for the real implementation.</summary>
public interface IFilePickerService
{
    /// <summary>Returns the picked file's local path, or null if the user
    /// cancelled. extensions are without the dot, e.g. "xlsx".</summary>
    Task<string?> PickFileAsync(string title, params string[] extensions);
}
