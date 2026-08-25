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
        services.AddSingleton<ITrustedDeviceStore, TrustedDeviceStore>();

        // A single shared IServerApiClient instance for the app's lifetime:
        // AddHttpClient<TClient>() registers TClient as transient, which
        // would hand each screen's ViewModel a fresh, unconfigured
        // HttpClient (BaseAddress unset) after SignInViewModel already
        // called Configure() on a different instance.
        services.AddHttpClient();
        services.AddSingleton<IServerApiClient>(sp =>
            new ServerApiClient(sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(ServerApiClient))));

        services.AddSingleton<MainWindowViewModel>();
        services.AddTransient<SignInViewModel>();
        services.AddTransient<InitialSetupViewModel>();
        services.AddTransient<DatabaseListViewModel>();
        services.AddTransient<CreateDatabaseViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<InventoryViewModel>();
        services.AddTransient<ChangePasswordViewModel>();

        services.AddSingleton<INavigationService, NavigationService>();

        Services = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindowViewModel = Services.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = new MainWindow { DataContext = mainWindowViewModel };

            _ = TryAutoSignInAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>"Trust this device" (see SignInViewModel/TrustedDeviceStore):
    /// if a saved session exists, is unexpired, and still validates against
    /// the server, skip straight to the database list. Otherwise fall back
    /// to the normal sign-in screen — this covers the token being revoked,
    /// the server having been reinstalled, the server being unreachable,
    /// etc., all of which should just look like "not signed in" rather than
    /// crashing the app on startup.</summary>
    private static async Task TryAutoSignInAsync()
    {
        var navigation = Services.GetRequiredService<INavigationService>();
        var trustedDeviceStore = Services.GetRequiredService<ITrustedDeviceStore>();
        var saved = trustedDeviceStore.Load();

        if (saved is not null)
        {
            try
            {
                var apiClient = Services.GetRequiredService<IServerApiClient>();
                apiClient.Configure(saved.ServerAddress);
                apiClient.SetSessionToken(saved.Token);

                await apiClient.GetHealthAsync();
                await apiClient.ListDatabasesAsync(); // also proves the token itself is still valid

                var session = Services.GetRequiredService<ClientSessionState>();
                session.ServerAddress = saved.ServerAddress;
                session.SessionToken = "set"; // presence flag only — see ClientSessionState

                navigation.ShowDatabaseList();
                return;
            }
            catch
            {
                trustedDeviceStore.Clear();
            }
        }

        navigation.ShowSignIn();
    }
}
