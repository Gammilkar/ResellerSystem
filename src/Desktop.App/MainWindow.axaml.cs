using Avalonia.Controls;
using ResellerSystem.Desktop.ViewModels;

namespace ResellerSystem.Desktop.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Closing += OnClosing;
    }

    // So table settings (per-row heights, column visibility, font size)
    // survive quitting the app directly (Alt+F4 / the X button) without
    // first navigating Back out of the screen that owns them.
    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (DataContext is MainWindowViewModel { CurrentViewModel: InventoryViewModel inventory })
        {
            inventory.PersistSettings();
        }
    }
}
