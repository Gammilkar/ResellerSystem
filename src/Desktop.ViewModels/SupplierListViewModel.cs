using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ResellerSystem.Desktop.Services.Api;
using ResellerSystem.Desktop.ViewModels.Navigation;
using ResellerSystem.Domain.Shared.Dto;

namespace ResellerSystem.Desktop.ViewModels;

/// <summary>Full "Поставщики" screen — list + purchase history for the
/// selected supplier + Create/Edit/Delete, all through SupplierEditDialog
/// (shared with SupplierPickerViewModel's inline "create new").</summary>
public sealed partial class SupplierListViewModel : ViewModelBase
{
    private readonly IServerApiClient _apiClient;
    private readonly IDialogService _dialogService;
    private readonly INavigationService _navigation;

    public SupplierListViewModel(IServerApiClient apiClient, IDialogService dialogService, INavigationService navigation)
    {
        _apiClient = apiClient;
        _dialogService = dialogService;
        _navigation = navigation;
    }

    public ObservableCollection<SupplierDto> Suppliers { get; } = new();
    public ObservableCollection<SupplierPurchaseHistoryRowDto> PurchaseHistory { get; } = new();

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private SupplierDto? _selectedSupplier;

    partial void OnSelectedSupplierChanged(SupplierDto? value) => _ = LoadHistoryAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        ErrorMessage = null;
        IsLoading = true;
        try
        {
            var list = await _apiClient.ListSuppliersAsync();
            Suppliers.Clear();
            foreach (var s in list) Suppliers.Add(s);
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

    private async Task LoadHistoryAsync()
    {
        PurchaseHistory.Clear();
        if (SelectedSupplier is null) return;

        try
        {
            var history = await _apiClient.GetSupplierPurchaseHistoryAsync(SelectedSupplier.Id);
            foreach (var h in history) PurchaseHistory.Add(h);
        }
        catch (ServerApiException ex)
        {
            ErrorMessage = ex.Error.Message;
        }
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        var dialogVm = new SupplierEditDialogViewModel(_apiClient);
        var created = await _dialogService.ShowAsync<SupplierEditDialogViewModel, SupplierDto>(dialogVm);
        if (created is not null) await LoadAsync();
    }

    [RelayCommand]
    private async Task EditAsync()
    {
        if (SelectedSupplier is null) return;

        var dialogVm = new SupplierEditDialogViewModel(_apiClient, SelectedSupplier);
        var updated = await _dialogService.ShowAsync<SupplierEditDialogViewModel, SupplierDto>(dialogVm);
        if (updated is not null) await LoadAsync();
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedSupplier is null) return;

        ErrorMessage = null;
        try
        {
            await _apiClient.DeleteSupplierAsync(SelectedSupplier.Id);
            await LoadAsync();
        }
        catch (ServerApiException ex)
        {
            ErrorMessage = ex.Error.Message;
        }
    }

    [RelayCommand]
    private void Back() => _navigation.ShowDashboard();
}
