using Avalonia.Controls;
using Avalonia.Interactivity;
using ResellerSystem.Desktop.ViewModels;
using ResellerSystem.Domain.Shared.Dto;

namespace ResellerSystem.Desktop.App.Views;

public partial class InventoryView : UserControl
{
    public InventoryView()
    {
        InitializeComponent();
    }

    // ComboBox.SelectedValue's binding is one-way (see InventoryView.axaml —
    // InventoryTableRowDto's properties are init-only, so nothing can bind
    // back into them), so committing an edit goes through SelectionChanged
    // here instead. This also fires once on initial bind and whenever a
    // row container gets recycled during scrolling; both cases select the
    // row's own current value, so the "no real change" guard in
    // InventoryViewModel's commands (comparing against the row's current
    // field) is what actually prevents spurious API calls, not this handler.
    private void StatusCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox { DataContext: InventoryTableRowDto row, SelectedValue: string code }) return;
        if (DataContext is InventoryViewModel vm) vm.UpdateStatusCommand.Execute((row, code));
    }

    private void PurchaseTypeCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox { DataContext: InventoryTableRowDto row, SelectedValue: string code }) return;
        if (DataContext is InventoryViewModel vm) vm.UpdatePurchaseTypeCommand.Execute((row, code));
    }

    // Marketplace/Место продажи are AutoCompleteBoxes (free text with
    // suggestions) rather than ComboBox — Avalonia's ComboBox has no
    // IsEditable/free-text mode (confirmed against the assembly: no Text
    // property), unlike WPF's. LostFocus catches both a picked suggestion
    // and freely typed text in one place.
    private void ListingMarketplaceCombo_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is not AutoCompleteBox { DataContext: InventoryTableRowDto row } box) return;
        var text = box.Text?.Trim();
        if (string.IsNullOrEmpty(text)) return;
        if (DataContext is InventoryViewModel vm) vm.UpdateListingMarketplaceCommand.Execute((row, text));
    }

    private void SaleMarketplaceCombo_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is not AutoCompleteBox { DataContext: InventoryTableRowDto row } box) return;
        var text = box.Text?.Trim();
        if (string.IsNullOrEmpty(text)) return;
        if (DataContext is InventoryViewModel vm) vm.UpdateSaleMarketplaceCommand.Execute((row, text));
    }

    private void SalePriceBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is not TextBox { DataContext: InventoryTableRowDto row } box) return;
        if (!decimal.TryParse(box.Text, out var price)) return;
        if (DataContext is InventoryViewModel vm) vm.UpdateSalePriceCommand.Execute((row, price));
    }
}
