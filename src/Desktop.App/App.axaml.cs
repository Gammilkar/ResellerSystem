using System.Net.Http;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using ResellerSystem.Desktop.App.Navigation;
using ResellerSystem.Desktop.Services;
using ResellerSystem.Desktop.Services.Api;
using ResellerSystem.Desktop.ViewModels;
using ResellerSystem.Desktop.ViewModels.Navigation;

namespace ResellerSystem.Desktop.App;

public sealed class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();

        services.AddSingleton<ClientSessionState>();
        services.AddHttpClient<IServerApiClient, ServerApiClient>();

        services.AddSingleton<MainWindowViewModel>();
        services.AddTransient<ServerConnectionViewModel>();
        services.AddTransient<InitialSetupViewModel>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<DatabaseListViewModel>();
        services.AddTransient<CreateDatabaseViewModel>();
        services.AddTransient<SelectedDatabaseViewModel>();
        services.AddTransient<InventoryViewModel>();

        services.AddSingleton<INavigationService, NavigationService>();

        Services = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindowViewModel = Services.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = new MainWindow { DataContext = mainWindowViewModel };

            // First screen per Stage 1 spec: Server Connection.
            Services.GetRequiredService<INavigationService>().ShowServerConnection();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
