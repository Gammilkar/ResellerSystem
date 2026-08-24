using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ResellerSystem.Desktop.Services;
using ResellerSystem.Desktop.Services.Api;
using ResellerSystem.Desktop.ViewModels.Navigation;

namespace ResellerSystem.Desktop.ViewModels;

/// <summary>
/// First screen: enter a server address (e.g. "http://192.168.1.100:5000"),
/// connect, and show server version/status once reachable.
/// </summary>
public sealed partial class ServerConnectionViewModel : ViewModelBase
{
    private readonly IServerApiClient _apiClient;
    private readonly ClientSessionState _session;
    private readonly INavigationService _navigation;

    public ServerConnectionViewModel(IServerApiClient apiClient, ClientSessionState session, INavigationService navigation)
    {
        _apiClient = apiClient;
        _session = session;
        _navigation = navigation;

        ServerAddress = session.ServerAddress ?? "http://localhost:5000";
    }

    [ObservableProperty]
    private string _serverAddress = string.Empty;

    [ObservableProperty]
    private bool _isConnecting;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _connectedServerVersion;

    [ObservableProperty]
    private string? _connectedStatus;

    [RelayCommand]
    private async Task ConnectAsync()
    {
        ErrorMessage = null;
        IsConnecting = true;
        try
        {
            _apiClient.Configure(ServerAddress);
            var health = await _apiClient.GetHealthAsync();

            ConnectedServerVersion = health.ServerVersion;
            ConnectedStatus = health.Status;
            _session.ServerAddress = ServerAddress;

            var authStatus = await _apiClient.GetAuthStatusAsync();
            if (authStatus.NeedsInitialSetup)
            {
                _navigation.ShowInitialSetup();
            }
            else
            {
                _navigation.ShowLogin();
            }
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = $"Could not reach the server: {ex.Message}";
        }
        catch (ServerApiException ex)
        {
            ErrorMessage = $"Server error: {ex.Error.Message}";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Unexpected error: {ex.Message}";
        }
        finally
        {
            IsConnecting = false;
        }
    }
}
