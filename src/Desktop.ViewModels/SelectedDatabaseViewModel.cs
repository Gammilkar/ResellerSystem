using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ResellerSystem.Desktop.Services;
using ResellerSystem.Desktop.Services.Api;
using ResellerSystem.Desktop.ViewModels.Navigation;

namespace ResellerSystem.Desktop.ViewModels;

/// <summary>
/// Placeholder screen shown after selecting a database — stands in for the
/// future Dashboard (Stage 2+). Confirms the connection is fully working:
/// server version + tenant schema version, per the Stage 1 acceptance
/// criteria ("see versions of server/client/database schema").
/// </summary>
public sealed partial class SelectedDatabaseViewModel : ViewModelBase
{
    private readonly IServerApiClient _apiClient;
    private readonly ClientSessionState _session;
    private readonly INavigationService _navigation;

    public SelectedDatabaseViewModel(IServerApiClient apiClient, ClientSessionState session, INavigationService navigation)
    {
        _apiClient = apiClient;
        _session = session;
        _navigation = navigation;

        DatabaseName = session.SelectedDatabase?.Name ?? "(none)";
    }

    [ObservableProperty]
    private string _databaseName;

    [ObservableProperty]
    private string? _serverVersion;

    [ObservableProperty]
    private int _tenantSchemaVersion;

    [ObservableProperty]
    private string? _errorMessage;

    [RelayCommand]
    private async Task LoadAsync()
    {
        ErrorMessage = null;
        try
        {
            var version = await _apiClient.GetVersionAsync();
            ServerVersion = version.ServerVersion;
            TenantSchemaVersion = version.TenantSchemaVersion;
        }
        catch (ServerApiException ex)
        {
            ErrorMessage = $"Could not load version info: {ex.Error.Message}";
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
}
