using Avalonia.Controls;
using ResellerSystem.Desktop.ViewModels;
using ResellerSystem.Desktop.ViewModels.Navigation;

namespace ResellerSystem.Desktop.App.Navigation;

/// <summary>Window.ShowDialog&lt;TResult&gt;(owner) based — the app has a
/// single MainWindow (App.axaml.cs), used everywhere as the owner. Zero new
/// NuGet packages needed: Avalonia/Avalonia.Desktop already provide this.
/// Each dialog ViewModel type maps to the Window that hosts it via
/// windowFactories, registered once in App.axaml.cs.</summary>
public sealed class DialogService : IDialogService
{
    private readonly IReadOnlyDictionary<Type, Func<Window>> _windowFactories;
    private readonly Func<Window> _ownerResolver;

    public DialogService(IReadOnlyDictionary<Type, Func<Window>> windowFactories, Func<Window> ownerResolver)
    {
        _windowFactories = windowFactories;
        _ownerResolver = ownerResolver;
    }

    public Task<TResult?> ShowAsync<TViewModel, TResult>(TViewModel viewModel) where TViewModel : ViewModelBase
    {
        if (!_windowFactories.TryGetValue(typeof(TViewModel), out var factory))
            throw new InvalidOperationException($"No dialog window registered for {typeof(TViewModel).Name}.");

        var window = factory();
        window.DataContext = viewModel;
        return window.ShowDialog<TResult?>(_ownerResolver());
    }
}
