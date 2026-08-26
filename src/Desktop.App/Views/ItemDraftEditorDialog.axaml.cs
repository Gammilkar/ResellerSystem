using Avalonia.Controls;
using ResellerSystem.Desktop.ViewModels;

namespace ResellerSystem.Desktop.App.Views;

public partial class ItemDraftEditorDialog : Window
{
    public ItemDraftEditorDialog()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is ItemDraftEditorViewModel vm)
            {
                vm.RequestClose += result => Close(result);
            }
        };
    }
}
