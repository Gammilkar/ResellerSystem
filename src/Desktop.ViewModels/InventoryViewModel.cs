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
/// Display settings (font size, which columns show) persist locally via
/// ITableSettingsStore. Row height is not user-adjustable — each
/// DataGridTemplateColumn's TextBlock wraps (TextWrapping="Wrap"), and
/// Avalonia's DataGrid rows auto-size to the tallest wrapped cell, the
/// same "AutoFit Row Height" behavior Excel applies to wrapped text.
/// </summary>
public sealed partial class InventoryViewModel : ViewModelBase
{
    private const string TableKey = "inventory";
    private const double DefaultFontSize = 13;

    private readonly IServerApiClient _apiClient;
    private readonly ClientSessionState _session;
    private readonly ITableSettingsStore _settingsStore;
    private readonly INavigationService _navigation;

    public InventoryViewModel(IServerApiClient apiClient, ClientSessionState session, ITableSettingsStore settingsStore, INavigationService navigation)
    {
        _apiClient = apiClient;
        _session = session;
        _settingsStore = settingsStore;
        _navigation = navigation;

        LoadColumnSettings();
    }

    public ObservableCollection<InventoryTableRowDto> TableRows { get; } = new();

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

    [RelayCommand]
    private async Task LoadAsync()
    {
        ErrorMessage = null;
        IsLoading = true;
        try
        {
            var rows = await _apiClient.ListInventoryTableAsync();
            TableRows.Clear();
            foreach (var r in rows) TableRows.Add(r);
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
    private void ToggleNewPurchaseForm() => ShowNewPurchaseForm = !ShowNewPurchaseForm;

    [RelayCommand]
    private void ToggleSettingsPanel()
    {
        if (ShowSettingsPanel) PersistSettings(); // closing the panel commits the choices
        ShowSettingsPanel = !ShowSettingsPanel;
    }

    /// <summary>Called by MainWindow on window Closing, in addition to the
    /// Back button and settings-panel close, so table settings survive
    /// quitting the app without navigating away from Inventory first.</summary>
    public void PersistSettings() => SaveColumnSettings();

    private void LoadColumnSettings()
    {
        var saved = _settingsStore.Load(TableKey);
        if (saved is null) return;

        DataFontSize = saved.FontSize;

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
            [nameof(ShowDaysListedColumn)] = ShowDaysListedColumn
        };
        _settingsStore.Save(TableKey, new TableSettings(DataFontSize, visibility));
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
        PersistSettings(); // so column/font choices aren't lost even if the settings panel was never opened
        _navigation.ShowDashboard();
    }
}
