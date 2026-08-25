using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ResellerSystem.Desktop.Services;
using ResellerSystem.Desktop.Services.Api;
using ResellerSystem.Desktop.ViewModels.Navigation;
using ResellerSystem.Domain.Shared.Dto;

namespace ResellerSystem.Desktop.ViewModels;

/// <summary>
/// The main Inventory screen — Product Specification section 34: one
/// Excel-like grid (Table, bound in InventoryView.axaml to a DataGrid with
/// user-resizable/reorderable columns), one row per Item with its
/// Purchase/Listing/Sale data flattened in via
/// IServerApiClient.ListInventoryTableAsync (server-side:
/// InventoryTableReader). The X-Database-Id header is attached by
/// NavigationService.ShowInventory before this loads.
///
/// Display settings (font size, row height, which columns show) persist
/// locally via ITableSettingsStore. Avalonia's DataGrid has no drag
/// handle to resize an individual row (confirmed against the assembly —
/// only column-resize infrastructure exists there), so per-row height is
/// done the way any other cell value is set: each row is wrapped in
/// InventoryRowViewModel with its own RowHeight, editable through a
/// dedicated "Высота строки" column, and DataGridRow's Style binds Height
/// to it (DataGridRow's DataContext is the row item, same mechanism as
/// any other per-row binding).
/// </summary>
public sealed partial class InventoryViewModel : ViewModelBase
{
    private const string TableKey = "inventory";
    private const double DefaultFontSize = 13;
    private const double DefaultRowHeight = 32;

    private readonly IServerApiClient _apiClient;
    private readonly ClientSessionState _session;
    private readonly ITableSettingsStore _settingsStore;
    private readonly INavigationService _navigation;
    private Dictionary<Guid, double> _rowHeightOverrides = new();

    public InventoryViewModel(IServerApiClient apiClient, ClientSessionState session, ITableSettingsStore settingsStore, INavigationService navigation)
    {
        _apiClient = apiClient;
        _session = session;
        _settingsStore = settingsStore;
        _navigation = navigation;

        LoadColumnSettings();
    }

    public ObservableCollection<InventoryRowViewModel> TableRows { get; } = new();

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;

    // New purchase form fields
    [ObservableProperty] private string _newSourceName = string.Empty;
    [ObservableProperty] private string _newTotalAmount = "0.00";
    [ObservableProperty] private string _newItemName = string.Empty;
    [ObservableProperty] private string _newQuantity = "1";
    [ObservableProperty] private bool _isSavingPurchase;
    [ObservableProperty] private bool _showNewPurchaseForm;

    // Display settings
    [ObservableProperty] private bool _showSettingsPanel;
    [ObservableProperty] private double _dataFontSize = DefaultFontSize;

    /// <summary>Default height newly-loaded rows start at, and what
    /// "Apply to all rows" resets every row back to. Each row can still be
    /// changed individually afterward via the "Высота строки" column.</summary>
    [ObservableProperty] private double _rowHeight = DefaultRowHeight;

    public double HeaderFontSize => DataFontSize + 1;
    partial void OnDataFontSizeChanged(double value) => OnPropertyChanged(nameof(HeaderFontSize));

    // Column visibility — includes columns hidden by default (ItemNumber)
    // so "add other column types" has somewhere real to come from, not
    // just show/hide of the original 12.
    [ObservableProperty] private bool _showItemNumberColumn;
    [ObservableProperty] private bool _showNameColumn = true;
    [ObservableProperty] private bool _showStatusColumn = true;
    [ObservableProperty] private bool _showPurchaseDateColumn = true;
    [ObservableProperty] private bool _showPurchaseSourceColumn = true;
    [ObservableProperty] private bool _showPurchaseTypeColumn = true;
    [ObservableProperty] private bool _showCostBasisColumn = true;
    [ObservableProperty] private bool _showListingDateColumn = true;
    [ObservableProperty] private bool _showMarketplaceColumn = true;
    [ObservableProperty] private bool _showSaleDateColumn = true;
    [ObservableProperty] private bool _showSaleMarketplaceColumn = true;
    [ObservableProperty] private bool _showSalePriceColumn = true;
    [ObservableProperty] private bool _showDaysListedColumn = true;
    [ObservableProperty] private bool _showRowHeightColumn = true;

    [RelayCommand]
    private async Task LoadAsync()
    {
        ErrorMessage = null;
        IsLoading = true;
        try
        {
            var rows = await _apiClient.ListInventoryTableAsync();
            TableRows.Clear();
            foreach (var r in rows)
            {
                var height = _rowHeightOverrides.GetValueOrDefault(r.ItemId, RowHeight);
                TableRows.Add(new InventoryRowViewModel(r, height));
            }
        }
        catch (ServerApiException ex)
        {
            ErrorMessage = ex.Error.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ApplyRowHeightToAll()
    {
        foreach (var row in TableRows) row.RowHeight = RowHeight;
    }

    [RelayCommand]
    private void ToggleNewPurchaseForm() => ShowNewPurchaseForm = !ShowNewPurchaseForm;

    [RelayCommand]
    private void ToggleSettingsPanel()
    {
        if (ShowSettingsPanel) SaveColumnSettings(); // closing the panel commits the choices
        ShowSettingsPanel = !ShowSettingsPanel;
    }

    private void LoadColumnSettings()
    {
        var saved = _settingsStore.Load(TableKey);
        if (saved is null) return;

        DataFontSize = saved.FontSize;
        RowHeight = saved.RowHeight;
        _rowHeightOverrides = saved.RowHeightOverrides is { } overrides ? new Dictionary<Guid, double>(overrides) : new();

        bool Get(string key, bool fallback) => saved.ColumnVisibility.TryGetValue(key, out var v) ? v : fallback;
        ShowItemNumberColumn = Get(nameof(ShowItemNumberColumn), ShowItemNumberColumn);
        ShowNameColumn = Get(nameof(ShowNameColumn), ShowNameColumn);
        ShowStatusColumn = Get(nameof(ShowStatusColumn), ShowStatusColumn);
        ShowPurchaseDateColumn = Get(nameof(ShowPurchaseDateColumn), ShowPurchaseDateColumn);
        ShowPurchaseSourceColumn = Get(nameof(ShowPurchaseSourceColumn), ShowPurchaseSourceColumn);
        ShowPurchaseTypeColumn = Get(nameof(ShowPurchaseTypeColumn), ShowPurchaseTypeColumn);
        ShowCostBasisColumn = Get(nameof(ShowCostBasisColumn), ShowCostBasisColumn);
        ShowListingDateColumn = Get(nameof(ShowListingDateColumn), ShowListingDateColumn);
        ShowMarketplaceColumn = Get(nameof(ShowMarketplaceColumn), ShowMarketplaceColumn);
        ShowSaleDateColumn = Get(nameof(ShowSaleDateColumn), ShowSaleDateColumn);
        ShowSaleMarketplaceColumn = Get(nameof(ShowSaleMarketplaceColumn), ShowSaleMarketplaceColumn);
        ShowSalePriceColumn = Get(nameof(ShowSalePriceColumn), ShowSalePriceColumn);
        ShowDaysListedColumn = Get(nameof(ShowDaysListedColumn), ShowDaysListedColumn);
        ShowRowHeightColumn = Get(nameof(ShowRowHeightColumn), ShowRowHeightColumn);
    }

    private void SaveColumnSettings()
    {
        var visibility = new Dictionary<string, bool>
        {
            [nameof(ShowItemNumberColumn)] = ShowItemNumberColumn,
            [nameof(ShowNameColumn)] = ShowNameColumn,
            [nameof(ShowStatusColumn)] = ShowStatusColumn,
            [nameof(ShowPurchaseDateColumn)] = ShowPurchaseDateColumn,
            [nameof(ShowPurchaseSourceColumn)] = ShowPurchaseSourceColumn,
            [nameof(ShowPurchaseTypeColumn)] = ShowPurchaseTypeColumn,
            [nameof(ShowCostBasisColumn)] = ShowCostBasisColumn,
            [nameof(ShowListingDateColumn)] = ShowListingDateColumn,
            [nameof(ShowMarketplaceColumn)] = ShowMarketplaceColumn,
            [nameof(ShowSaleDateColumn)] = ShowSaleDateColumn,
            [nameof(ShowSaleMarketplaceColumn)] = ShowSaleMarketplaceColumn,
            [nameof(ShowSalePriceColumn)] = ShowSalePriceColumn,
            [nameof(ShowDaysListedColumn)] = ShowDaysListedColumn,
            [nameof(ShowRowHeightColumn)] = ShowRowHeightColumn
        };
        var rowHeights = TableRows.ToDictionary(r => r.ItemId, r => r.RowHeight);
        _settingsStore.Save(TableKey, new TableSettings(DataFontSize, RowHeight, visibility, rowHeights));
    }

    [RelayCommand]
    private async Task CreatePurchaseAsync()
    {
        ErrorMessage = null;

        if (!decimal.TryParse(NewTotalAmount, out var totalAmount) || totalAmount < 0)
        {
            ErrorMessage = "Total amount must be a non-negative number.";
            return;
        }
        if (!int.TryParse(NewQuantity, out var quantity) || quantity < 1)
        {
            ErrorMessage = "Quantity must be a whole number of at least 1.";
            return;
        }
        if (string.IsNullOrWhiteSpace(NewSourceName) || string.IsNullOrWhiteSpace(NewItemName))
        {
            ErrorMessage = "Source and item name are required.";
            return;
        }

        IsSavingPurchase = true;
        try
        {
            await _apiClient.CreatePurchaseAsync(new CreatePurchaseRequest
            {
                PurchaseDate = DateOnly.FromDateTime(DateTime.Today),
                SourceName = NewSourceName,
                TotalAmount = totalAmount,
                ItemName = NewItemName,
                Quantity = quantity
            });

            NewSourceName = string.Empty;
            NewTotalAmount = "0.00";
            NewItemName = string.Empty;
            NewQuantity = "1";
            ShowNewPurchaseForm = false;

            await LoadAsync();
        }
        catch (ServerApiException ex)
        {
            ErrorMessage = ex.Error.Message;
        }
        finally
        {
            IsSavingPurchase = false;
        }
    }

    [RelayCommand]
    private void Back()
    {
        SaveColumnSettings(); // so per-row height edits aren't lost even if the settings panel was never opened
        _navigation.ShowDashboard();
    }
}
