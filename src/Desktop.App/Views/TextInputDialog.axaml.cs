using Avalonia.Controls;
using ResellerSystem.Desktop.ViewModels;

namespace ResellerSystem.Desktop.App.Views;

public partial class TextInputDialog : Window
{
    public TextInputDialog()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is TextInputDialogViewModel vm)
            {
                vm.RequestClose += result => Close(result);
            }
        };
    }
}
