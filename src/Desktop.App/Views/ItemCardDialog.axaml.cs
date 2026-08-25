using Avalonia.Controls;
using ResellerSystem.Desktop.ViewModels;

namespace ResellerSystem.Desktop.App.Views;

public partial class ItemCardDialog : Window
{
    public ItemCardDialog()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is ItemCardDialogViewModel vm)
            {
                vm.RequestClose += result => Close(result);
            }
        };
    }
}
