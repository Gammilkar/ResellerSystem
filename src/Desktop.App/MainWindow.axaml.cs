using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using ResellerSystem.Desktop.Services;
using ResellerSystem.Desktop.ViewModels;

namespace ResellerSystem.Desktop.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Closing += OnClosing;
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        // So table settings (per-row heights, column visibility, font size)
        // survive quitting the app directly (Alt+F4 / the X button) without
        // first navigating Back out of the screen that owns them.
        if (DataContext is MainWindowViewModel { CurrentViewModel: InventoryViewModel inventory })
        {
            inventory.PersistSettings();
        }

        // Width/Height while WindowState is Maximized report the maximized
        // bounds, not the size to restore to — but Avalonia (like most UI
        // frameworks) keeps the pre-maximize bounds as the window's Width/
        // Height internally and only changes what's rendered, so setting
        // WindowState back to Maximized on the next launch after restoring
        // these same numbers still lands on a sane un-maximized size later.
        var isMaximized = WindowState == WindowState.Maximized;
        App.Services.GetRequiredService<IWindowSettingsStore>().Save(new WindowSettings(Width, Height, isMaximized));
    }
}
