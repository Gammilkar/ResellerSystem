using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ResellerSystem.Desktop.Services.Api;
using ResellerSystem.Domain.Shared.Dto;

namespace ResellerSystem.Desktop.ViewModels;

/// <summary>The one place the supplier create/edit form is defined — used
/// both standalone (SupplierListView's Новый/Редактировать buttons) and
/// nested from SupplierPickerViewModel's "Создать нового", so the CRUD
/// form itself is never duplicated between the two entry points.</summary>
public sealed partial class SupplierEditDialogViewModel : ViewModelBase
{
    private readonly IServerApiClient _apiClient;
    private readonly Guid? _supplierId;

    public SupplierEditDialogViewModel(IServerApiClient apiClient, SupplierDto? existing = null)
    {
        _apiClient = apiClient;
        _supplierId = existing?.Id;
        Title = existing is null ? "Новый поставщик" : "Редактировать поставщика";
        _name = existing?.Name ?? string.Empty;
        _phone = existing?.Phone;
        _email = existing?.Email;
        _address = existing?.Address;
        _notes = existing?.Notes;
    }

    public string Title { get; }

    /// <summary>The saved SupplierDto, or null if the user cancelled.</summary>
    public event Action<SupplierDto?>? RequestClose;

    [ObservableProperty] private string _name;
    [ObservableProperty] private string? _phone;
    [ObservableProperty] private string? _email;
    [ObservableProperty] private string? _address;
    [ObservableProperty] private string? _notes;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isSaving;

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "Название обязательно.";
            return;
        }

        IsSaving = true;
        ErrorMessage = null;
        try
        {
            var result = _supplierId is { } id
                ? await _apiClient.UpdateSupplierAsync(id, new UpdateSupplierRequest { Name = Name, Phone = Phone, Email = Email, Address = Address, Notes = Notes })
                : await _apiClient.CreateSupplierAsync(new CreateSupplierRequest { Name = Name, Phone = Phone, Email = Email, Address = Address, Notes = Notes });
            RequestClose?.Invoke(result);
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
    private void Cancel() => RequestClose?.Invoke(null);
}
