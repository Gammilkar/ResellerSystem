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

    public void ShowServerConnection() => SetCurrent(_services.GetRequiredService<ServerConnectionViewModel>());

    public void ShowInitialSetup() => SetCurrent(_services.GetRequiredService<InitialSetupViewModel>());

    public void ShowLogin() => SetCurrent(_services.GetRequiredService<LoginViewModel>());

    public void ShowDatabaseList()
    {
        var vm = _services.GetRequiredService<DatabaseListViewModel>();
        SetCurrent(vm);
        _ = vm.LoadCommand.ExecuteAsync(null);
    }

    public void ShowCreateDatabase() => SetCurrent(_services.GetRequiredService<CreateDatabaseViewModel>());

    public void ShowSelectedDatabase()
    {
        var vm = _services.GetRequiredService<SelectedDatabaseViewModel>();
        SetCurrent(vm);
        _ = vm.LoadCommand.ExecuteAsync(null);
    }

    public void ShowInventory()
    {
        var apiClient = _services.GetRequiredService<Services.Api.IServerApiClient>();
        var session = _services.GetRequiredService<ClientSessionState>();
        apiClient.SetDatabaseId(session.SelectedDatabase?.Id);

        var vm = _services.GetRequiredService<InventoryViewModel>();
        SetCurrent(vm);
        _ = vm.LoadCommand.ExecuteAsync(null);
    }

    private void SetCurrent(ViewModelBase viewModel)
    {
        _services.GetRequiredService<MainWindowViewModel>().CurrentViewModel = viewModel;
    }
}
