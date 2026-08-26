using Avalonia.Controls;
using ResellerSystem.Desktop.ViewModels;
using ResellerSystem.Domain.Shared.Dto;

namespace ResellerSystem.Desktop.App.Views;

public partial class PurchaseListView : UserControl
{
    public PurchaseListView()
    {
        InitializeComponent();
    }

    // DataGrid's multi-selection (SelectionMode="Extended") has no
    // ViewModel-observable equivalent, so mirror it into
    // PurchaseListViewModel.SelectedRows here — mirrors InventoryView's
    // MainGrid_SelectionChanged pattern exactly.
    private void MainGrid_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not PurchaseListViewModel vm) return;
        vm.SelectedRows.Clear();
        foreach (var item in MainGrid.SelectedItems)
        {
            if (item is PurchaseListRowDto row) vm.SelectedRows.Add(row);
        }
    }
}
