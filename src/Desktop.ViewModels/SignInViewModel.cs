using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ResellerSystem.Desktop.Services;
using ResellerSystem.Desktop.Services.Api;
using ResellerSystem.Desktop.ViewModels.Navigation;

namespace ResellerSystem.Desktop.ViewModels;

/// <summary>
/// Single combined sign-in screen: server address + username + password,
/// replacing the old two-step ServerConnection -> Login flow. "Trust this
/// device" persists the session (see ITrustedDeviceStore) so the app can
/// skip straight to the database list on next launch — App.axaml.cs tries
/// that before ever showing this screen.
/// </summary>
public sealed partial class SignInViewModel : ViewModelBase
{
    private readonly IServerApiClient _apiClient;
    private readonly ClientSessionState _session;
    private readonly ITrustedDeviceStore _trustedDeviceStore;
    private readonly INavigationService _navigation;

    public SignInViewModel(IServerApiClient apiClient, ClientSessionState session, ITrustedDeviceStore trustedDeviceStore, INavigationService navigation)
    {
        _apiClient = apiClient;
        _session = session;
        _trustedDeviceStore = trustedDeviceStore;
        _navigation = navigation;

        ServerAddress = session.ServerAddress ?? "http://localhost:5000";
    }

    [ObservableProperty]
    private string _serverAddress = string.Empty;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private bool _rememberMe;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    [RelayCommand]
    private async Task SignInAsync()
    {
        ErrorMessage = null;
        IsBusy = true;
        try
        {
            _apiClient.Configure(ServerAddress);
            await _apiClient.GetHealthAsync();
            _session.ServerAddress = ServerAddress;

            var authStatus = await _apiClient.GetAuthStatusAsync();
            if (authStatus.NeedsInitialSetup)
            {
                _navigation.ShowInitialSetup();
                return;
            }

            var login = await _apiClient.LoginAsync(Username, Password, RememberMe);

            if (RememberMe)
            {
                _trustedDeviceStore.Save(new TrustedDeviceSession(ServerAddress, login.Token, login.ExpiresAt));
            }
            else
            {
                _trustedDeviceStore.Clear();
            }

            _navigation.ShowDatabaseList();
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = $"Could not reach the server: {ex.Message}";
        }
        catch (ServerApiException ex)
        {
            ErrorMessage = ex.Error.Message;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Unexpected error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
