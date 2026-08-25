namespace ResellerSystem.Desktop.ViewModels.Navigation;

/// <summary>Lets view models open a modal dialog without depending on
/// Avalonia/Window types — mirrors INavigationService's UI-free pattern.
/// The caller constructs the dialog's ViewModel itself (with whatever
/// dependencies it needs) and gets back whatever the dialog closed with.</summary>
public interface IDialogService
{
    Task<TResult?> ShowAsync<TViewModel, TResult>(TViewModel viewModel) where TViewModel : ViewModelBase;
}
