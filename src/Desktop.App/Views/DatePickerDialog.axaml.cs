using Avalonia.Controls;
using Avalonia.Interactivity;
using ResellerSystem.Desktop.ViewModels;

namespace ResellerSystem.Desktop.App.Views;

public partial class DatePickerDialog : Window
{
    public DatePickerDialog()
    {
        InitializeComponent();

        // Set directly on the control rather than via a bound property —
        // this is the "read/write the control itself" half of the fix
        // described in DatePickerDialogViewModel's doc comment.
        DataContextChanged += (_, _) =>
        {
            if (DataContext is DatePickerDialogViewModel vm)
            {
                Picker.SelectedDate = vm.InitialDate;
            }
        };
    }

    private void Ok_Click(object? sender, RoutedEventArgs e)
    {
        var date = Picker.SelectedDate;
        Close(date is { } d ? DateOnly.FromDateTime(d) : (DateOnly?)null);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(null);
}
