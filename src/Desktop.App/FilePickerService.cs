using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using ResellerSystem.Desktop.Services;

namespace ResellerSystem.Desktop.App;

public sealed class FilePickerService : IFilePickerService
{
    public async Task<string?> PickFileAsync(string title, params string[] extensions)
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop
            || desktop.MainWindow is not Window window)
        {
            return null;
        }

        var topLevel = TopLevel.GetTopLevel(window);
        if (topLevel is null) return null;

        var patterns = extensions.Select(e => $"*.{e}").ToArray();
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType(string.Join("/", extensions).ToUpperInvariant()) { Patterns = patterns }
            }
        });

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }
}
