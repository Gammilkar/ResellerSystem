using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ResellerSystem.Desktop.ViewModels;
using ResellerSystem.Domain.Shared.Dto;

namespace ResellerSystem.Desktop.App.Views;

public partial class InventoryView : UserControl
{
    // Must match the DataGridTemplateColumn declaration order in
    // InventoryView.axaml exactly — CanUserReorderColumns lets the user
    // drag columns to a different DISPLAY order, but the underlying
    // DataGrid.Columns collection stays in this original definition order
    // regardless, so indexing by it (not by DisplayIndex) survives reordering.
    private static readonly string[] ColumnKeys =
    {
        "ItemNumber", "Name", "Status", "PurchaseDate", "PurchaseSource", "PurchaseType",
        "CostBasis", "ListingDate", "Marketplace", "SaleDate", "SaleMarketplace", "SalePrice", "DaysListed"
    };

    private bool _columnWidthsWired;

    public InventoryView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => InitializeColumnWidths();
    }

    /// <summary>Applies any previously-saved column widths, then wires each
    /// column to keep InventoryViewModel.ColumnWidths live-updated as the
    /// user drags a resize handle — SaveColumnSettings (already called from
    /// Back/settings-panel-close/window-closing) just persists whatever is
    /// in that dictionary at the time, so no extra "capture on close" step
    /// is needed here.</summary>
    private void InitializeColumnWidths()
    {
        if (_columnWidthsWired) return;
        if (DataContext is not InventoryViewModel vm) return;
        _columnWidthsWired = true;

        for (var i = 0; i < MainGrid.Columns.Count && i < ColumnKeys.Length; i++)
        {
            var key = ColumnKeys[i];
            var column = MainGrid.Columns[i];

            if (vm.ColumnWidths.TryGetValue(key, out var savedWidth))
            {
                column.Width = new DataGridLength(savedWidth);
            }

            column.PropertyChanged += (_, e) =>
            {
                if (e.Property == DataGridColumn.WidthProperty && column.Width.IsAbsolute)
                {
                    vm.ColumnWidths[key] = column.Width.DisplayValue;
                }
            };
        }
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

    // Marketplace/Место продажи pair a real ComboBox (visible dropdown
    // arrow, click to see the full preset list) with a plain TextBox that
    // holds the actual/custom value — Avalonia's ComboBox has no editable/
    // free-text mode (confirmed against the assembly), so picking a preset
    // just types it into the TextBox next to it rather than being bound
    // directly to the row.
    private void ListingMarketplacePreset_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox { SelectedItem: string preset } combo) return;
        if (FindSiblingTextBox(combo) is not { } textBox) return;
        textBox.Text = preset;

        if (textBox.DataContext is InventoryTableRowDto row && DataContext is InventoryViewModel vm)
        {
            vm.UpdateListingMarketplaceCommand.Execute((row, preset));
        }
        combo.SelectedItem = null; // so the same preset can be picked again later
    }

    private void SaleMarketplacePreset_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox { SelectedItem: string preset } combo) return;
        if (FindSiblingTextBox(combo) is not { } textBox) return;
        textBox.Text = preset;

        if (textBox.DataContext is InventoryTableRowDto row && DataContext is InventoryViewModel vm)
        {
            vm.UpdateSaleMarketplaceCommand.Execute((row, preset));
        }
        combo.SelectedItem = null;
    }

    private static TextBox? FindSiblingTextBox(Control control) =>
        (control.Parent as Panel)?.Children.OfType<TextBox>().FirstOrDefault();

    private void ListingMarketplaceCombo_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is not TextBox { DataContext: InventoryTableRowDto row } box) return;
        var text = box.Text?.Trim();
        if (string.IsNullOrEmpty(text)) return;
        if (DataContext is InventoryViewModel vm) vm.UpdateListingMarketplaceCommand.Execute((row, text));
    }

    private void SaleMarketplaceCombo_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is not TextBox { DataContext: InventoryTableRowDto row } box) return;
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
