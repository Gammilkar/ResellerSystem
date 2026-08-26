using System.Net.Http;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using ResellerSystem.Desktop.App.Navigation;
using ResellerSystem.Desktop.App.Views;
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
        services.AddSingleton<IFilePickerService, FilePickerService>();
        services.AddSingleton<ITableSettingsStore, TableSettingsStore>();
        services.AddSingleton<IWindowSettingsStore, WindowSettingsStore>();

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
        services.AddTransient<ImportViewModel>();
        services.AddTransient<SupplierListViewModel>();

        services.AddSingleton<INavigationService, NavigationService>();

        // Every modal dialog Window this app opens is registered here, once —
        // see IDialogService's doc comment for why Window.ShowDialog was
        // chosen over an in-window overlay pattern.
        services.AddSingleton<IDialogService>(sp => new DialogService(
            new Dictionary<Type, Func<Window>>
            {
                [typeof(ItemCardDialogViewModel)] = () => new ItemCardDialog(),
                [typeof(DatePickerDialogViewModel)] = () => new DatePickerDialog(),
                [typeof(SupplierEditDialogViewModel)] = () => new SupplierEditDialog(),
                [typeof(SupplierPickerViewModel)] = () => new SupplierPickerDialog(),
                [typeof(ConfirmDialogViewModel)] = () => new ConfirmDialog()
            },
            () => (Window)((IClassicDesktopStyleApplicationLifetime)ApplicationLifetime!).MainWindow!));

        Services = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindowViewModel = Services.GetRequiredService<MainWindowViewModel>();
            var mainWindow = new MainWindow { DataContext = mainWindowViewModel };

            var savedWindow = Services.GetRequiredService<IWindowSettingsStore>().Load();
            if (savedWindow is not null)
            {
                mainWindow.Width = savedWindow.Width;
                mainWindow.Height = savedWindow.Height;
                if (savedWindow.IsMaximized) mainWindow.WindowState = WindowState.Maximized;
            }

            desktop.MainWindow = mainWindow;

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

        // Never let anything in here leave the window blank (see
        // MainWindow.axaml's ContentControl — CurrentViewModel staying
        // null renders nothing at all, with no visible error). Whatever
        // goes wrong, fall back to the plain sign-in screen.
        try
        {
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
        }
        catch (Exception ex)
        {
            LogStartupError(ex);
        }

        navigation.ShowSignIn();
    }

    private static void LogStartupError(Exception ex)
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ResellerSystem Client", "logs");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "startup-errors.log"), $"[{DateTimeOffset.UtcNow:o}] {ex}\n\n");
        }
        catch
        {
            // Logging itself must never be the reason startup fails.
        }
    }
}
