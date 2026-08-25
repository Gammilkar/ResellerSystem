using Avalonia.Controls;
using ResellerSystem.Desktop.ViewModels;

namespace ResellerSystem.Desktop.App.Views;

public partial class SupplierEditDialog : Window
{
    public SupplierEditDialog()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is SupplierEditDialogViewModel vm)
            {
                vm.RequestClose += result => Close(result);
            }
        };
    }
}
