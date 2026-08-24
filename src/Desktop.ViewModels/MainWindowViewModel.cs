using CommunityToolkit.Mvvm.ComponentModel;

namespace ResellerSystem.Desktop.ViewModels;

/// <summary>
/// Shell view model. Hosts whichever screen is currently active; Avalonia's
/// DataTemplates (see Desktop.App/ViewLocator) pick the matching View for
/// whatever ViewModelBase-derived instance is set here.
/// </summary>
public sealed partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private ViewModelBase? _currentViewModel;
}
