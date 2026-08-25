using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ResellerSystem.Desktop.Services.Api;
using ResellerSystem.Domain.Shared.Dto;

namespace ResellerSystem.Desktop.ViewModels;

/// <summary>"Карточка товара" — opened by clicking a row's Наименование
/// cell (see InventoryViewModel.OpenItemCardCommand). Edits the fields
/// already supported by the existing UpdateItemAsync/PATCH
/// /api/v1/inventory/items/{id} — nothing new server-side was needed for
/// this dialog, only the UI.</summary>
public sealed partial class ItemCardDialogViewModel : ViewModelBase
{
    private readonly IServerApiClient _apiClient;
    private readonly Guid _itemId;

    public ItemCardDialogViewModel(Guid itemId, IServerApiClient apiClient)
    {
        _itemId = itemId;
        _apiClient = apiClient;
        _ = LoadAsync();
    }

    public IReadOnlyList<StatusOptions.Option> StatusOptionsList => StatusOptions.All;

    /// <summary>Fired with true (saved) or false (cancelled) — the hosting
    /// Window (ItemCardDialog.axaml.cs) closes itself with this as the
    /// ShowDialog&lt;bool&gt; result.</summary>
    public event Action<bool>? RequestClose;

    [ObservableProperty] private bool _isLoading = true;
    [ObservableProperty] private bool _isSaving;
    [ObservableProperty] private string? _errorMessage;

    [ObservableProperty] private long _itemNumber;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string? _categoryName;
    [ObservableProperty] private string _status = "InStock";
    [ObservableProperty] private decimal _costBasisCalculated;

    /// <summary>Blank means "no override" — saving with this blank does
    /// NOT clear an existing override (UpdateItemRequest's PATCH semantics
    /// treat null as "leave alone"); it just leaves whatever was there.</summary>
    [ObservableProperty] private string _costBasisOverrideText = string.Empty;
    [ObservableProperty] private string? _notes;

    private async Task LoadAsync()
    {
        try
        {
            var item = await _apiClient.GetItemAsync(_itemId);
            ItemNumber = item.ItemNumber;
            Name = item.Name;
            CategoryName = item.CategoryName;
            Status = item.Status;
            CostBasisCalculated = item.CostBasisCalculated;
            CostBasisOverrideText = item.CostBasisOverride?.ToString() ?? string.Empty;
            Notes = item.Notes;
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
    private async Task SaveAsync()
    {
        decimal? costBasisOverride = null;
        if (!string.IsNullOrWhiteSpace(CostBasisOverrideText))
        {
            if (!decimal.TryParse(CostBasisOverrideText, out var parsed))
            {
                ErrorMessage = "Итоговая себестоимость должна быть числом.";
                return;
            }
            costBasisOverride = parsed;
        }

        IsSaving = true;
        ErrorMessage = null;
        try
        {
            await _apiClient.UpdateItemAsync(_itemId, new UpdateItemRequest
            {
                Name = Name,
                CategoryName = CategoryName,
                Status = Status,
                CostBasisOverride = costBasisOverride,
                Notes = Notes
            });
            RequestClose?.Invoke(true);
        }
        catch (ServerApiException ex)
        {
            ErrorMessage = ex.Error.Message;
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(false);
}
