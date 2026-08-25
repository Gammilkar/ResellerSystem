using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ResellerSystem.Desktop.ViewModels;

/// <summary>One reusable dialog for Дата покупки/Дата публикации/Дата
/// продажи — parameterized by Title rather than three bespoke dialogs.
/// CalendarDatePicker gives both a calendar popup and manual typed entry
/// in the same control.</summary>
public sealed partial class DatePickerDialogViewModel : ViewModelBase
{
    public DatePickerDialogViewModel(string title, DateOnly? initialDate)
    {
        Title = title;
        _selectedDate = initialDate is { } d ? new DateTimeOffset(d.ToDateTime(TimeOnly.MinValue)) : DateTimeOffset.Now;
    }

    public string Title { get; }

    [ObservableProperty] private DateTimeOffset? _selectedDate;

    /// <summary>Null means "cancelled" — the caller should not save anything.</summary>
    public event Action<DateOnly?>? RequestClose;

    [RelayCommand]
    private void Ok() => RequestClose?.Invoke(SelectedDate is { } d ? DateOnly.FromDateTime(d.Date) : null);

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(null);
}
