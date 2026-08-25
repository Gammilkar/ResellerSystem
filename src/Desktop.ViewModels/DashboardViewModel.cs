using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ResellerSystem.Desktop.Services;
using ResellerSystem.Desktop.Services.Api;
using ResellerSystem.Desktop.ViewModels.Navigation;
using ResellerSystem.Domain.Shared.Dto;

namespace ResellerSystem.Desktop.ViewModels;

/// <summary>
/// Landing screen after selecting a database — Product Specification
/// section 22 ("Dashboard"). Everything shown is a live snapshot from
/// GET /api/v1/dashboard/summary, recomputed on every load; nothing here
/// is stored or cached client-side.
/// </summary>
public sealed partial class DashboardViewModel : ViewModelBase
{
    private readonly IServerApiClient _apiClient;
    private readonly ClientSessionState _session;
    private readonly ITrustedDeviceStore _trustedDeviceStore;
    private readonly INavigationService _navigation;

    public DashboardViewModel(IServerApiClient apiClient, ClientSessionState session, ITrustedDeviceStore trustedDeviceStore, INavigationService navigation)
    {
        _apiClient = apiClient;
        _session = session;
        _trustedDeviceStore = trustedDeviceStore;
        _navigation = navigation;

        DatabaseName = session.SelectedDatabase?.Name ?? "(none)";
    }

    public ObservableCollection<InventoryAgingRowDto> InventoryAging { get; } = new();

    [ObservableProperty]
    private string _databaseName;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private decimal _inventoryOnHandCostBasis;

    [ObservableProperty]
    private int _inventoryOnHandCount;

    [ObservableProperty]
    private decimal _netProfitAllTime;

    [ObservableProperty]
    private decimal _netProfitThisMonth;

    [ObservableProperty]
    private decimal _netProfitThisWeek;

    [ObservableProperty]
    private int _itemsSoldAllTime;

    [ObservableProperty]
    private int _itemsSoldThisMonth;

    [ObservableProperty]
    private int _itemsSoldThisWeek;

    [ObservableProperty]
    private decimal _grossSalesAllTime;

    [ObservableProperty]
    private decimal? _averageRoiPercent;

    [ObservableProperty]
    private double? _averageDaysToSell;

    [RelayCommand]
    private async Task LoadAsync()
    {
        ErrorMessage = null;
        IsLoading = true;
        try
        {
            var summary = await _apiClient.GetDashboardSummaryAsync();

            InventoryOnHandCostBasis = summary.InventoryOnHandCostBasis;
            InventoryOnHandCount = summary.InventoryOnHandCount;
            NetProfitAllTime = summary.NetProfitAllTime;
            NetProfitThisMonth = summary.NetProfitThisMonth;
            NetProfitThisWeek = summary.NetProfitThisWeek;
            ItemsSoldAllTime = summary.ItemsSoldAllTime;
            ItemsSoldThisMonth = summary.ItemsSoldThisMonth;
            ItemsSoldThisWeek = summary.ItemsSoldThisWeek;
            GrossSalesAllTime = summary.GrossSalesAllTime;
            AverageRoiPercent = summary.AverageRoiPercent;
            AverageDaysToSell = summary.AverageDaysToSell;

            InventoryAging.Clear();
            foreach (var bucket in summary.InventoryAging) InventoryAging.Add(bucket);
        }
        catch (ServerApiException ex)
        {
            ErrorMessage = $"Could not load dashboard: {ex.Error.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ChangeDatabase()
    {
        _session.SelectedDatabase = null;
        _navigation.ShowDatabaseList();
    }

    [RelayCommand]
    private void OpenInventory() => _navigation.ShowInventory();

    [RelayCommand]
    private void OpenImport() => _navigation.ShowImport();

    [RelayCommand]
    private void OpenSuppliers() => _navigation.ShowSuppliers();

    [RelayCommand]
    private void ChangePassword() => _navigation.ShowChangePassword();

    [RelayCommand]
    private async Task SignOutAsync()
    {
        await _apiClient.LogoutAsync();
        _trustedDeviceStore.Clear();
        _session.SessionToken = null;
        _session.SelectedDatabase = null;
        _navigation.ShowSignIn();
    }
}
