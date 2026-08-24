using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ResellerSystem.Desktop.Services;
using ResellerSystem.Desktop.Services.Api;
using ResellerSystem.Desktop.ViewModels.Navigation;
using ResellerSystem.Domain.Shared.Dto;

namespace ResellerSystem.Desktop.ViewModels;

/// <summary>
/// "Create Database" screen. TimeZone is pre-filled from the local machine's
/// time zone but the user can change it; Currency defaults to USD.
/// </summary>
public sealed partial class CreateDatabaseViewModel : ViewModelBase
{
    private readonly IServerApiClient _apiClient;
    private readonly ClientSessionState _session;
    private readonly INavigationService _navigation;

    public CreateDatabaseViewModel(IServerApiClient apiClient, ClientSessionState session, INavigationService navigation)
    {
        _apiClient = apiClient;
        _session = session;
        _navigation = navigation;

        // Best-effort local time zone detection — user can override.
        TimeZone = TryGetLocalIanaTimeZone();
    }

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _timeZone;

    [ObservableProperty]
    private string _currency = "USD";

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private string? _errorMessage;

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;
        IsSaving = true;
        try
        {
            var created = await _apiClient.CreateDatabaseAsync(new CreateDatabaseRequest
            {
                Name = Name,
                TimeZone = TimeZone,
                Currency = Currency
            });

            _session.SelectedDatabase = created;
            _navigation.ShowSelectedDatabase();
        }
        catch (ServerApiException ex)
        {
            ErrorMessage = ex.Error.Details.Count > 0
                ? string.Join(" ", ex.Error.Details)
                : ex.Error.Message;
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private void Cancel() => _navigation.ShowDatabaseList();

    private static string TryGetLocalIanaTimeZone()
    {
        try
        {
            var local = TimeZoneInfo.Local;
            if (TimeZoneInfo.TryConvertWindowsIdToIanaId(local.Id, out var iana))
            {
                return iana;
            }
            return local.Id; // already IANA on Linux/macOS
        }
        catch
        {
            return "UTC";
        }
    }
}
