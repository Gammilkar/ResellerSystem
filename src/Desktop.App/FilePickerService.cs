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

        var options = new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        };

        // With no extensions given (e.g. attaching an arbitrary document to
        // a Purchase/Item), building a filter with an empty Patterns array
        // makes Avalonia's Win32 file dialog call the native SetFileTypes
        // with a zero-length filter spec, which COM rejects with
        // E_INVALIDARG — an unhandled COMException on the UI thread that
        // crashes the whole process. Omitting FileTypeFilter entirely lets
        // the dialog show "All files", which is also the correct behavior
        // here (no specific extension is expected).
        if (extensions.Length > 0)
        {
            var patterns = extensions.Select(e => $"*.{e}").ToArray();
            options.FileTypeFilter = new[]
            {
                new FilePickerFileType(string.Join("/", extensions).ToUpperInvariant()) { Patterns = patterns }
            };
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(options);

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }
}
