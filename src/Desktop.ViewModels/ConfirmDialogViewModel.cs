using CommunityToolkit.Mvvm.Input;

namespace ResellerSystem.Desktop.ViewModels;

/// <summary>Generic Yes/No confirmation — reusable anywhere a destructive
/// or hard-to-reverse action needs the user to confirm first.</summary>
public sealed partial class ConfirmDialogViewModel : ViewModelBase
{
    public ConfirmDialogViewModel(string title, string message, string confirmText = "Да", string cancelText = "Отмена")
    {
        Title = title;
        Message = message;
        ConfirmText = confirmText;
        CancelText = cancelText;
    }

    public string Title { get; }
    public string Message { get; }
    public string ConfirmText { get; }
    public string CancelText { get; }

    public event Action<bool>? RequestClose;

    [RelayCommand]
    private void Confirm() => RequestClose?.Invoke(true);

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(false);
}
