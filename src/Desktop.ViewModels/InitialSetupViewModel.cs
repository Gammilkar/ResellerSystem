using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ResellerSystem.Desktop.Services;
using ResellerSystem.Desktop.Services.Api;
using ResellerSystem.Desktop.ViewModels.Navigation;

namespace ResellerSystem.Desktop.ViewModels;

/// <summary>
/// Shown only when the server reports NeedsInitialSetup = true (no admin
/// account exists). In the packaged product this normally never appears —
/// Server.Host auto-creates the initial admin at first startup (see
/// StartupChecks.EnsureInitialAdminAsync) so the installer stays fully
/// unattended. This screen exists for the dev-mode / manual-setup path.
/// </summary>
public sealed partial class InitialSetupViewModel : ViewModelBase
{
    private readonly IServerApiClient _apiClient;
    private readonly INavigationService _navigation;

    public InitialSetupViewModel(IServerApiClient apiClient, INavigationService navigation)
    {
        _apiClient = apiClient;
        _navigation = navigation;
    }

    [ObservableProperty]
    private string _username = "admin";

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _confirmPassword = string.Empty;

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private string? _errorMessage;

    [RelayCommand]
    private async Task CreateAccountAsync()
    {
        ErrorMessage = null;

        if (Password.Length < 8)
        {
            ErrorMessage = "Password must be at least 8 characters.";
            return;
        }
        if (Password != ConfirmPassword)
        {
            ErrorMessage = "Passwords do not match.";
            return;
        }

        IsSaving = true;
        try
        {
            await _apiClient.InitialSetupAsync(Username, Password);
            await _apiClient.LoginAsync(Username, Password);
            _navigation.ShowDatabaseList();
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
}
