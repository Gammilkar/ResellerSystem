using System.Collections.ObjectModel;
using System.Linq;
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
    private readonly IDialogService _dialogService;

    public PurchaseListViewModel(IServerApiClient apiClient, INavigationService navigation, IDialogService dialogService)
    {
        _apiClient = apiClient;
        _navigation = navigation;
        _dialogService = dialogService;
        SelectedRows.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(DeleteSelectedLabel));
        };
    }

    public ObservableCollection<PurchaseListRowDto> Purchases { get; } = new();

    /// <summary>Kept in sync by PurchaseListView's code-behind (DataGrid
    /// SelectionChanged) since the grid's own multi-selection isn't
    /// something a ViewModel can observe directly.</summary>
    public ObservableCollection<PurchaseListRowDto> SelectedRows { get; } = new();

    public bool HasSelection => SelectedRows.Count > 0;
    public string DeleteSelectedLabel => SelectedRows.Count > 0 ? $"Удалить выбранные ({SelectedRows.Count})" : "Удалить выбранные";

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
    private async Task DeleteSelectedAsync()
    {
        if (SelectedRows.Count == 0) return;

        var confirmVm = new ConfirmDialogViewModel(
            "Удалить выбранные поступления?",
            $"Будет удалено поступлений: {SelectedRows.Count}. Связанные товары также будут удалены. Это действие нельзя отменить из интерфейса.",
            confirmText: "Удалить");
        var confirmed = await _dialogService.ShowAsync<ConfirmDialogViewModel, bool>(confirmVm);
        if (!confirmed) return;

        ErrorMessage = null;
        var errors = new List<string>();
        foreach (var row in SelectedRows.ToList())
        {
            try
            {
                await _apiClient.DeletePurchaseFullAsync(row.Id);
            }
            catch (ServerApiException ex)
            {
                errors.Add($"{row.PurchaseDate}: {ex.Error.Message}");
            }
        }
        if (errors.Count > 0) ErrorMessage = string.Join(" | ", errors);

        SelectedRows.Clear();
        await LoadAsync();
    }

    [RelayCommand]
    private void CreateNew() => _navigation.ShowPurchaseEdit(null);

    [RelayCommand]
    private void Back() => _navigation.ShowDashboard();
}
