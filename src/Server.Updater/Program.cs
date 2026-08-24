using System.Diagnostics;

namespace ResellerSystem.Server.Updater;

/// <summary>
/// Standalone console app, launched elevated by UpdateService
/// (Server.Data/Update/UpdateService.cs) once a new server package has
/// been downloaded, checksum-verified, and a mandatory backup taken.
///
/// Server.Host cannot stop/replace/restart itself mid-request, so this
/// separate process does it:
///
///   1. Stop the ResellerSystemServer Windows Service.
///   2. Extract the new version's zip into
///      {InstallDir}\server-versions\{newVersion}\
///   3. Swap the {InstallDir}\server directory symlink to point at the
///      new version folder (old version folder is left on disk untouched —
///      cheap, instant rollback target).
///   4. Start the service again (binPath is always {InstallDir}\server\
///      Server.Host.exe, so the service registration itself never changes
///      between versions).
///   5. Poll http://localhost:{port}/health until healthy or timeout.
///   6. On success: record the new version as current, exit 0.
///      On failure: swap the symlink back to the previous version, start
///      the OLD service again, and exit non-zero.
///
/// KNOWN LIMITATION (see KNOWN_LIMITATIONS.md): step 6's failure path only
/// rolls back FILES. If the new version's Server.Host already applied
/// database migrations during its (failed) startup attempt, those
/// migrations are NOT automatically reverted — the old code may now be
/// running against a newer schema. The pre-update backup (id printed
/// below) must be restored manually from Server Manager in that case.
/// Automating that too is future work, not implemented here.
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var options = ParseArgs(args);
        if (options is null)
        {
            Console.Error.WriteLine("Usage: Server.Updater --install-dir <dir> --package <zip> --version <x.y.z> [--service-name <name>] [--port <n>] [--backup-id <id>] [--health-timeout-seconds <n>]");
            return 2;
        }

        Log($"Starting update to version {options.Version}. Install dir: {options.InstallDir}");

        var versionsRoot = Path.Combine(options.InstallDir, "server-versions");
        var serverLinkPath = Path.Combine(options.InstallDir, "server");
        var currentVersionFile = Path.Combine(versionsRoot, "current.txt");

        string? previousVersion = File.Exists(currentVersionFile)
            ? (await File.ReadAllTextAsync(currentVersionFile)).Trim()
            : null;
        Log($"Previous version on record: {previousVersion ?? "(none)"}");

        try
        {
            Log("Step 1/6: stopping service...");
            RunScCommand("stop", options.ServiceName);
            await Task.Delay(2000);

            Log("Step 2/6: extracting new version package...");
            var newVersionDir = Path.Combine(versionsRoot, options.Version);
            if (Directory.Exists(newVersionDir)) Directory.Delete(newVersionDir, recursive: true);
            Directory.CreateDirectory(newVersionDir);
            System.IO.Compression.ZipFile.ExtractToDirectory(options.PackagePath, newVersionDir);

            Log("Step 3/6: swapping server -> new version...");
            SwapServerLink(serverLinkPath, newVersionDir);

            Log("Step 4/6: starting service on new version...");
            RunScCommand("start", options.ServiceName);

            Log("Step 5/6: waiting for /health...");
            var healthy = await WaitForHealthyAsync(options.Port, options.HealthTimeoutSeconds);

            if (healthy)
            {
                Log("Step 6/6: update successful — recording new current version.");
                Directory.CreateDirectory(versionsRoot);
                await File.WriteAllTextAsync(currentVersionFile, options.Version);
                Log($"Update to {options.Version} completed successfully.");
                return 0;
            }

            Log("Health check failed — rolling back files to previous version.");
            return await RollBackAsync(options, versionsRoot, serverLinkPath, previousVersion);
        }
        catch (Exception ex)
        {
            Log($"ERROR during update: {ex.Message}");
            Log("Attempting file-level rollback due to unexpected error...");
            return await RollBackAsync(options, versionsRoot, serverLinkPath, previousVersion);
        }
    }

    private static async Task<int> RollBackAsync(UpdaterOptions options, string versionsRoot, string serverLinkPath, string? previousVersion)
    {
        if (previousVersion is null)
        {
            Log("No previous version on record — cannot roll back files. Manual intervention required.");
            return 1;
        }

        try
        {
            RunScCommand("stop", options.ServiceName);
            await Task.Delay(2000);

            var previousVersionDir = Path.Combine(versionsRoot, previousVersion);
            SwapServerLink(serverLinkPath, previousVersionDir);

            RunScCommand("start", options.ServiceName);
            var healthy = await WaitForHealthyAsync(options.Port, options.HealthTimeoutSeconds);

            Log(healthy
                ? $"Rolled back to {previousVersion} successfully."
                : $"Rolled back files to {previousVersion} but the service did not report healthy — check logs.");

            if (options.BackupId is not null)
            {
                Log($"IMPORTANT: if the failed update already applied database migrations, " +
                    $"restore backup '{options.BackupId}' from Server Manager > Backups to fully revert. " +
                    $"This is NOT done automatically — see KNOWN_LIMITATIONS.md.");
            }

            return 1; // update failed overall, even though rollback itself may have succeeded
        }
        catch (Exception ex)
        {
            Log($"ERROR during rollback: {ex.Message}. Manual intervention required.");
            return 1;
        }
    }

    private static void SwapServerLink(string linkPath, string targetDir)
    {
        if (Directory.Exists(linkPath) || File.Exists(linkPath))
        {
            // Deletes only the link/junction itself, not its target, as
            // long as this is in fact a reparse point (symlink) and not a
            // real directory — which is always true here after the first
            // successful install, since this method is the only thing that
            // ever creates {InstallDir}\server.
            var info = new DirectoryInfo(linkPath);
            if (info.LinkTarget is not null)
            {
                Directory.Delete(linkPath, recursive: false);
            }
            else
            {
                // First-ever run: {InstallDir}\server might still be a real
                // directory from the original installer (pre-Update-Engine
                // layout). Move it aside rather than destroying it blindly.
                var backupPath = linkPath + ".pre-update-engine-backup";
                if (!Directory.Exists(backupPath)) Directory.Move(linkPath, backupPath);
            }
        }

        Directory.CreateSymbolicLink(linkPath, targetDir);
    }

    private static void RunScCommand(string verb, string serviceName)
    {
        var psi = new ProcessStartInfo("sc.exe", $"{verb} \"{serviceName}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using var process = Process.Start(psi);
        process?.WaitForExit(30000);
    }

    private static async Task<bool> WaitForHealthyAsync(int port, int timeoutSeconds)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var body = await http.GetStringAsync($"http://localhost:{port}/health");
                if (body.Contains("\"healthy\"", StringComparison.OrdinalIgnoreCase)) return true;
            }
            catch
            {
                // still starting up — keep polling
            }
            await Task.Delay(2000);
        }
        return false;
    }

    private static void Log(string message) =>
        Console.WriteLine($"[{DateTime.UtcNow:O}] {message}");

    private sealed record UpdaterOptions(
        string InstallDir, string PackagePath, string Version,
        string ServiceName, int Port, string? BackupId, int HealthTimeoutSeconds);

    private static UpdaterOptions? ParseArgs(string[] args)
    {
        string? installDir = null, packagePath = null, version = null, backupId = null;
        var serviceName = "ResellerSystemServer";
        var port = 5000;
        var healthTimeout = 120;

        for (var i = 0; i < args.Length - 1; i++)
        {
            switch (args[i])
            {
                case "--install-dir": installDir = args[++i]; break;
                case "--package": packagePath = args[++i]; break;
                case "--version": version = args[++i]; break;
                case "--service-name": serviceName = args[++i]; break;
                case "--port": port = int.Parse(args[++i]); break;
                case "--backup-id": backupId = args[++i]; break;
                case "--health-timeout-seconds": healthTimeout = int.Parse(args[++i]); break;
            }
        }

        if (installDir is null || packagePath is null || version is null) return null;
        return new UpdaterOptions(installDir, packagePath, version, serviceName, port, backupId, healthTimeout);
    }
}
