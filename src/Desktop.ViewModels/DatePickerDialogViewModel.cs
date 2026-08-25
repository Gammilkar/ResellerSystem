namespace ResellerSystem.Desktop.ViewModels;

/// <summary>One reusable dialog for Дата покупки/Дата публикации/Дата
/// продажи — parameterized by Title rather than three bespoke dialogs.
/// Purely a display-state holder: the actual selected date is read
/// directly off the CalendarDatePicker control in DatePickerDialog's
/// code-behind at OK-click time (not round-tripped through a bound
/// property here) — a two-way SelectedDate binding turned out to be
/// unreliable in practice, silently keeping the dialog's initial value
/// no matter what the user picked.</summary>
public sealed class DatePickerDialogViewModel : ViewModelBase
{
    public DatePickerDialogViewModel(string title, DateOnly? initialDate)
    {
        Title = title;
        InitialDate = initialDate?.ToDateTime(TimeOnly.MinValue) ?? DateTime.Today;
    }

    public string Title { get; }
    public DateTime InitialDate { get; }
}
