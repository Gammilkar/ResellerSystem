using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ResellerSystem.Desktop.Services;
using ResellerSystem.Desktop.Services.Api;
using ResellerSystem.Desktop.ViewModels.Navigation;
using ResellerSystem.Domain.Shared.Dto;

namespace ResellerSystem.Desktop.ViewModels;

/// <summary>
/// First business-module screen — proves the whole chain end-to-end:
/// Desktop.App -> Server API (/api/v1/inventory/...) -> Modules.Inventory
/// -> tenant database, with the selected DatabaseProfileDto's Id sent as
/// X-Database-Id on every request (see ServerApiClient.SetDatabaseId,
/// called from ShowInventory in NavigationService).
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

    public ObservableCollection<PurchaseDto> Purchases { get; } = new();
    public ObservableCollection<ItemDto> Items { get; } = new();

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;

    // New purchase form fields
    [ObservableProperty] private string _newSourceName = string.Empty;
    [ObservableProperty] private string _newTotalAmount = "0.00";
    [ObservableProperty] private string _newItemName = string.Empty;
    [ObservableProperty] private string _newQuantity = "1";
    [ObservableProperty] private bool _isSavingPurchase;

    [RelayCommand]
    private async Task LoadAsync()
    {
        ErrorMessage = null;
        IsLoading = true;
        try
        {
            var purchases = await _apiClient.ListPurchasesAsync();
            Purchases.Clear();
            foreach (var p in purchases) Purchases.Add(p);

            var items = await _apiClient.ListItemsAsync(status: null);
            Items.Clear();
            foreach (var i in items) Items.Add(i);
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
