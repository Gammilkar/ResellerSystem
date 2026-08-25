using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Avalonia;

namespace ResellerSystem.Desktop.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Product Specification section 86: the app's currency is always
        // USD, regardless of the Windows OS locale it happens to run
        // under. Without this, {0:C} bindings (and unculture-qualified
        // decimal.TryParse on number entry fields) silently follow
        // CurrentCulture — e.g. render "1 234,56 ₽" and refuse to parse
        // "150.00" on a Russian-locale Windows install. Must run before
        // any thread (including Avalonia's UI thread) is created.
        var usCulture = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.DefaultThreadCurrentCulture = usCulture;
        CultureInfo.DefaultThreadCurrentUICulture = usCulture;

        var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ResellerSystem Client", "logs");
        Directory.CreateDirectory(logDir);

        // .LogToTrace() below (Avalonia's internal diagnostics — binding
        // errors, XAML load failures, etc.) goes nowhere without a
        // listener; without this, a startup failure in there is
        // completely silent (blank window, no crash, no clue).
        Trace.Listeners.Add(new TextWriterTraceListener(Path.Combine(logDir, "avalonia-trace.log")));
        Trace.AutoFlush = true;

        AppDomain.CurrentDomain.UnhandledException += (_, e) => LogFatal(logDir, e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) => { LogFatal(logDir, e.Exception); e.SetObserved(); };

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            LogFatal(logDir, ex);
            throw;
        }
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .WithInterFont()
        .LogToTrace();

    private static void LogFatal(string logDir, Exception? ex)
    {
        if (ex is null) return;
        try
        {
            File.AppendAllText(Path.Combine(logDir, "startup-errors.log"), $"[{DateTimeOffset.UtcNow:o}] {ex}\n\n");
        }
        catch
        {
            // Logging itself must never be the reason startup fails.
        }
    }
}
