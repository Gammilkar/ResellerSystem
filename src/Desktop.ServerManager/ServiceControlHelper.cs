using System.Diagnostics;

namespace ResellerSystem.Desktop.ServerManager;

/// <summary>
/// Server Manager itself runs un-elevated (so it can just sit in the tray
/// without a UAC prompt on every login). Starting/stopping a Windows
/// Service requires admin rights, so each action shells out to sc.exe with
/// the "runas" verb — Windows shows a single UAC prompt only for that
/// action, not for the whole app.
/// </summary>
public static class ServiceControlHelper
{
    public static Task<bool> StartAsync(string serviceName) => RunScAsync("start", serviceName);
    public static Task<bool> StopAsync(string serviceName) => RunScAsync("stop", serviceName);

    public static async Task<bool> RestartAsync(string serviceName)
    {
        var stopped = await RunScAsync("stop", serviceName);
        if (!stopped) return false;
        await Task.Delay(1500);
        return await RunScAsync("start", serviceName);
    }

    private static Task<bool> RunScAsync(string verb, string serviceName)
    {
        return Task.Run(() =>
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "sc.exe",
                    Arguments = $"{verb} \"{serviceName}\"",
                    UseShellExecute = true,
                    Verb = "runas", // triggers a UAC prompt for this action only
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true
                };
                using var process = Process.Start(psi);
                process?.WaitForExit(15000);
                return process is { ExitCode: 0 };
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // User declined the UAC prompt.
                return false;
            }
        });
    }
}
