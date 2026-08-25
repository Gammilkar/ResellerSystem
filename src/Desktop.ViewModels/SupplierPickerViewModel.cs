using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ResellerSystem.Desktop.Services.Api;
using ResellerSystem.Desktop.ViewModels.Navigation;
using ResellerSystem.Domain.Shared.Dto;

namespace ResellerSystem.Desktop.ViewModels;

/// <summary>Opened from the Inventory grid's Место покупки cell — search +
/// pick an existing supplier, or create one inline via SupplierEditDialog
/// (the same form SupplierListView uses, not a second copy of it).</summary>
public sealed partial class SupplierPickerViewModel : ViewModelBase
{
    private readonly IServerApiClient _apiClient;
    private readonly IDialogService _dialogService;

    public SupplierPickerViewModel(IServerApiClient apiClient, IDialogService dialogService)
    {
        _apiClient = apiClient;
        _dialogService = dialogService;
        _ = LoadAsync();
    }

    public ObservableCollection<SupplierDto> Suppliers { get; } = new();
    public ObservableCollection<SupplierDto> FilteredSuppliers { get; } = new();

    /// <summary>The chosen supplier, or null if the user cancelled.</summary>
    public event Action<SupplierDto?>? RequestClose;

    [ObservableProperty] private string _filterText = string.Empty;
    [ObservableProperty] private SupplierDto? _selectedSupplier;
    [ObservableProperty] private bool _isLoading = true;
    [ObservableProperty] private string? _errorMessage;

    partial void OnFilterTextChanged(string value) => ApplyFilter();

    private async Task LoadAsync()
    {
        try
        {
            var list = await _apiClient.ListSuppliersAsync();
            Suppliers.Clear();
            foreach (var s in list) Suppliers.Add(s);
            ApplyFilter();
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

    private void ApplyFilter()
    {
        FilteredSuppliers.Clear();
        var matches = string.IsNullOrWhiteSpace(FilterText)
            ? Suppliers
            : Suppliers.Where(s => s.Name.Contains(FilterText, StringComparison.OrdinalIgnoreCase));
        foreach (var s in matches) FilteredSuppliers.Add(s);
    }

    [RelayCommand]
    private async Task CreateNewAsync()
    {
        var dialogVm = new SupplierEditDialogViewModel(_apiClient);
        var created = await _dialogService.ShowAsync<SupplierEditDialogViewModel, SupplierDto>(dialogVm);
        if (created is null) return;

        Suppliers.Add(created);
        ApplyFilter();
        SelectedSupplier = created;
    }

    [RelayCommand]
    private void Select()
    {
        if (SelectedSupplier is null) return;
        RequestClose?.Invoke(SelectedSupplier);
    }

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(null);
}
