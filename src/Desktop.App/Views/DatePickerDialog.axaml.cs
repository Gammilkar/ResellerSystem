using Avalonia.Controls;
using ResellerSystem.Desktop.ViewModels;

namespace ResellerSystem.Desktop.App.Views;

public partial class DatePickerDialog : Window
{
    public DatePickerDialog()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is DatePickerDialogViewModel vm)
            {
                vm.RequestClose += result => Close(result);
            }
        };
    }
}
