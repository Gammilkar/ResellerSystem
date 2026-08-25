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
/// </summary>
public sealed partial class InventoryViewModel : ViewModelBase
{
    private readonly IServerApiClient _apiClient;
    private readonly ClientSessionState _session;
    private readonly INavigationService _navigation;

    public InventoryViewModel(IServerApiClient apiClient, ClientSessionState session, INavigationService navigation)
    {
        _apiClient = apiClient;
        _session = session;
        _navigation = navigation;
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
    private void Back() => _navigation.ShowDashboard();
}
