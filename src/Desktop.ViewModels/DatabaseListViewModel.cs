using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ResellerSystem.Desktop.Services;
using ResellerSystem.Desktop.Services.Api;
using ResellerSystem.Desktop.ViewModels.Navigation;
using ResellerSystem.Domain.Shared.Dto;
using ResellerSystem.Domain.Shared.Enums;

namespace ResellerSystem.Desktop.ViewModels;

/// <summary>
/// Shows the list of tenant databases on the connected server ("Main
/// Business", "Daria", "Test", ...). Only the display Name and status are
/// shown — the physical PostgreSQL database name never reaches the client.
/// </summary>
public sealed partial class DatabaseListViewModel : ViewModelBase
{
    private readonly IServerApiClient _apiClient;
    private readonly ClientSessionState _session;
    private readonly INavigationService _navigation;

    public DatabaseListViewModel(IServerApiClient apiClient, ClientSessionState session, INavigationService navigation)
    {
        _apiClient = apiClient;
        _session = session;
        _navigation = navigation;
    }

    public ObservableCollection<DatabaseProfileDto> Databases { get; } = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private DatabaseProfileDto? _selected;

    [RelayCommand]
    private async Task LoadAsync()
    {
        ErrorMessage = null;
        IsLoading = true;
        try
        {
            var databases = await _apiClient.ListDatabasesAsync();
            Databases.Clear();
            foreach (var db in databases)
            {
                Databases.Add(db);
            }
        }
        catch (ServerApiException ex)
        {
            ErrorMessage = $"Could not load databases: {ex.Error.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void Open()
    {
        if (Selected is null) return;
        if (Selected.Status != DatabaseStatusDto.Ready || !Selected.IsActive)
        {
            ErrorMessage = $"Database '{Selected.Name}' is not ready (status: {Selected.Status}).";
            return;
        }

        _session.SelectedDatabase = Selected;
        _navigation.ShowDashboard();
    }

    [RelayCommand]
    private void CreateNew() => _navigation.ShowCreateDatabase();

    [RelayCommand]
    private void ChangeServer() => _navigation.ShowServerConnection();
}
