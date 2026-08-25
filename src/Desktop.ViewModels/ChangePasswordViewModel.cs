using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ResellerSystem.Desktop.Services.Api;
using ResellerSystem.Desktop.ViewModels.Navigation;

namespace ResellerSystem.Desktop.ViewModels;

/// <summary>Reachable from the Dashboard — lets a signed-in user change
/// their own password without touching the server's credentials file.</summary>
public sealed partial class ChangePasswordViewModel : ViewModelBase
{
    private readonly IServerApiClient _apiClient;
    private readonly INavigationService _navigation;

    public ChangePasswordViewModel(IServerApiClient apiClient, INavigationService navigation)
    {
        _apiClient = apiClient;
        _navigation = navigation;
    }

    [ObservableProperty]
    private string _currentPassword = string.Empty;

    [ObservableProperty]
    private string _newPassword = string.Empty;

    [ObservableProperty]
    private string _confirmNewPassword = string.Empty;

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _successMessage;

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;
        SuccessMessage = null;

        if (NewPassword.Length < 8)
        {
            ErrorMessage = "New password must be at least 8 characters.";
            return;
        }
        if (NewPassword != ConfirmNewPassword)
        {
            ErrorMessage = "New passwords do not match.";
            return;
        }

        IsSaving = true;
        try
        {
            await _apiClient.ChangePasswordAsync(CurrentPassword, NewPassword);

            CurrentPassword = string.Empty;
            NewPassword = string.Empty;
            ConfirmNewPassword = string.Empty;
            SuccessMessage = "Password changed.";
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
    private void Back() => _navigation.ShowDashboard();
}
