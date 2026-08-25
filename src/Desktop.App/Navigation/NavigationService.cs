using Microsoft.Extensions.DependencyInjection;
using ResellerSystem.Desktop.Services;
using ResellerSystem.Desktop.ViewModels;
using ResellerSystem.Desktop.ViewModels.Navigation;

namespace ResellerSystem.Desktop.App.Navigation;

public sealed class NavigationService : INavigationService
{
    private readonly IServiceProvider _services;

    public NavigationService(IServiceProvider services)
    {
        _services = services;
    }

    public void ShowSignIn() => SetCurrent(_services.GetRequiredService<SignInViewModel>());

    public void ShowInitialSetup() => SetCurrent(_services.GetRequiredService<InitialSetupViewModel>());

    public void ShowChangePassword() => SetCurrent(_services.GetRequiredService<ChangePasswordViewModel>());

    public void ShowDatabaseList()
    {
        var vm = _services.GetRequiredService<DatabaseListViewModel>();
        SetCurrent(vm);
        _ = vm.LoadCommand.ExecuteAsync(null);
    }

    public void ShowCreateDatabase() => SetCurrent(_services.GetRequiredService<CreateDatabaseViewModel>());

    public void ShowDashboard()
    {
        SetDatabaseIdFromSession();

        var vm = _services.GetRequiredService<DashboardViewModel>();
        SetCurrent(vm);
        _ = vm.LoadCommand.ExecuteAsync(null);
    }

    public void ShowInventory()
    {
        SetDatabaseIdFromSession();

        var vm = _services.GetRequiredService<InventoryViewModel>();
        SetCurrent(vm);
        _ = vm.LoadCommand.ExecuteAsync(null);
    }

    // Every screen scoped to a selected tenant database (Dashboard,
    // Inventory, and future Sales/Returns/Listings/... screens) needs
    // X-Database-Id attached before it calls the API — set it here rather
    // than duplicating this in each Show*() method.
    private void SetDatabaseIdFromSession()
    {
        var apiClient = _services.GetRequiredService<Services.Api.IServerApiClient>();
        var session = _services.GetRequiredService<ClientSessionState>();
        apiClient.SetDatabaseId(session.SelectedDatabase?.Id);
    }

    private void SetCurrent(ViewModelBase viewModel)
    {
        _services.GetRequiredService<MainWindowViewModel>().CurrentViewModel = viewModel;
    }
}
