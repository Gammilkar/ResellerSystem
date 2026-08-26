using Avalonia.Controls;
using Avalonia.Input;
using ResellerSystem.Desktop.ViewModels;

namespace ResellerSystem.Desktop.App.Views;

public partial class PurchaseEditView : UserControl
{
    public PurchaseEditView()
    {
        InitializeComponent();
    }

    // Double-clicking a row opens the full Item Draft Editor instead of
    // relying on inline cell editing (the grid itself is IsReadOnly="True")
    // — matches Add/Edit both going through the same rich dialog.
    private void ItemsGrid_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (ItemsGrid.SelectedItem is not PurchaseLineEditViewModel line) return;
        if (DataContext is PurchaseEditViewModel vm) vm.EditItemLineCommand.Execute(line);
    }
}
