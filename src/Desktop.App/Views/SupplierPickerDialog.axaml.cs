using Avalonia.Controls;
using ResellerSystem.Desktop.ViewModels;

namespace ResellerSystem.Desktop.App.Views;

public partial class SupplierPickerDialog : Window
{
    public SupplierPickerDialog()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is SupplierPickerViewModel vm)
            {
                vm.RequestClose += result => Close(result);
            }
        };
    }
}
