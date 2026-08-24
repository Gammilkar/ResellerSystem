using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ResellerSystem.Desktop.Services;
using ResellerSystem.Desktop.Services.Api;
using ResellerSystem.Desktop.ViewModels.Navigation;

namespace ResellerSystem.Desktop.ViewModels;

public sealed partial class LoginViewModel : ViewModelBase
{
    private readonly IServerApiClient _apiClient;
    private readonly ClientSessionState _session;
    private readonly INavigationService _navigation;

    public LoginViewModel(IServerApiClient apiClient, ClientSessionState session, INavigationService navigation)
    {
        _apiClient = apiClient;
        _session = session;
        _navigation = navigation;
    }

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private bool _isLoggingIn;

    [ObservableProperty]
    private string? _errorMessage;

    [RelayCommand]
    private async Task LoginAsync()
    {
        ErrorMessage = null;
        IsLoggingIn = true;
        try
        {
            await _apiClient.LoginAsync(Username, Password);
            // ServerApiClient.LoginAsync already attached the token to the
            // HttpClient; keep a copy in session state too so it can be
            // restored if a new IServerApiClient instance is ever created.
            _session.SessionToken = "set"; // presence flag only — token itself lives in HttpClient, never persisted to disk
            _navigation.ShowDatabaseList();
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
            IsLoggingIn = false;
        }
    }

    [RelayCommand]
    private void ChangeServer() => _navigation.ShowServerConnection();
}
