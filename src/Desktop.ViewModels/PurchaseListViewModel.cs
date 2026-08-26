using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ResellerSystem.Desktop.Services.Api;
using ResellerSystem.Desktop.ViewModels.Navigation;
using ResellerSystem.Domain.Shared.Dto;

namespace ResellerSystem.Desktop.ViewModels;

/// <summary>"Purchases" screen (Product Specification §25-26) — list +
/// filters over the full purchase-intake workflow's Purchases, default
/// sorted by Purchase Date descending.</summary>
public sealed partial class PurchaseListViewModel : ViewModelBase
{
    private readonly IServerApiClient _apiClient;
    private readonly INavigationService _navigation;

    public PurchaseListViewModel(IServerApiClient apiClient, INavigationService navigation)
    {
        _apiClient = apiClient;
        _navigation = navigation;
    }

    public ObservableCollection<PurchaseListRowDto> Purchases { get; } = new();

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;

    // Filters
    [ObservableProperty] private string? _filterSourceName;
    [ObservableProperty] private string? _filterPurchaseType;
    [ObservableProperty] private string? _filterPaymentMethod;
    [ObservableProperty] private string? _filterSearch;
    [ObservableProperty] private DateOnly? _filterDateFrom;
    [ObservableProperty] private DateOnly? _filterDateTo;

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var filter = new PurchaseListFilterRequest
            {
                DateFrom = FilterDateFrom,
                DateTo = FilterDateTo,
                SourceName = string.IsNullOrWhiteSpace(FilterSourceName) ? null : FilterSourceName,
                PurchaseType = string.IsNullOrWhiteSpace(FilterPurchaseType) ? null : FilterPurchaseType,
                PaymentMethod = string.IsNullOrWhiteSpace(FilterPaymentMethod) ? null : FilterPaymentMethod,
                Search = string.IsNullOrWhiteSpace(FilterSearch) ? null : FilterSearch
            };
            var rows = await _apiClient.ListPurchasesFullAsync(filter);
            Purchases.Clear();
            foreach (var r in rows) Purchases.Add(r);
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
    private void ClearFilters()
    {
        FilterSourceName = null;
        FilterPurchaseType = null;
        FilterPaymentMethod = null;
        FilterSearch = null;
        FilterDateFrom = null;
        FilterDateTo = null;
        _ = LoadAsync();
    }

    [RelayCommand]
    private void OpenPurchase(PurchaseListRowDto row) => _navigation.ShowPurchaseEdit(row.Id);

    [RelayCommand]
    private void CreateNew() => _navigation.ShowPurchaseEdit(null);

    [RelayCommand]
    private void Back() => _navigation.ShowDashboard();
}
