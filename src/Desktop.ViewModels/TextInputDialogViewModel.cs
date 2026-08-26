using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ResellerSystem.Desktop.ViewModels;

/// <summary>Generic single-line text prompt — used for "+ Add" on any
/// reference list (Purchase Source, Category, Payment Method, Expense
/// Type, ...) without needing a bespoke dialog per list.</summary>
public sealed partial class TextInputDialogViewModel : ViewModelBase
{
    public TextInputDialogViewModel(string title, string message, string initialValue = "")
    {
        Title = title;
        Message = message;
        _value = initialValue;
    }

    public string Title { get; }
    public string Message { get; }

    [ObservableProperty] private string _value;

    public event Action<string?>? RequestClose;

    [RelayCommand]
    private void Ok() => RequestClose?.Invoke(string.IsNullOrWhiteSpace(Value) ? null : Value.Trim());

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(null);
}
