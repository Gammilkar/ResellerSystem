using System.Net.NetworkInformation;
using System.ServiceProcess;
using System.Text.Json;

namespace ResellerSystem.Desktop.ServerManager;

public sealed class ServerStatusSnapshot
{
    public required string ServiceStatus { get; init; }       // "Running" | "Stopped" | "Not installed" | ...
    public required string PostgresServiceStatus { get; init; }
    public string? ServerVersion { get; init; }
    public string? HealthStatus { get; init; }
    public required string LocalIpAddress { get; init; }
    public required int Port { get; init; }
    public required string StorageLocation { get; init; }
    public required long FreeDiskSpaceBytes { get; init; }
}

/// <summary>
/// Pulls together everything the Server Manager UI shows, from three
/// sources: Windows Service Control Manager (via ServiceController — no
/// admin rights needed just to *query* status), the local appsettings.json
/// (for storage path/port), and the server's own /health endpoint.
/// </summary>
public sealed class ServerStatusReader
{
    private const string ServiceName = "ResellerSystemServer";
    private const string PgServiceName = "ResellerSystemPostgreSQL";

    private readonly string _installDir;

    public ServerStatusReader(string installDir)
    {
        _installDir = installDir;
    }

    public async Task<ServerStatusSnapshot> ReadAsync(CancellationToken ct = default)
    {
        var port = ReadPortFromConfig();
        var storageLocation = ReadStorageRootFromConfig();

        string? version = null;
        string? health = null;
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var healthJson = await http.GetStringAsync($"http://localhost:{port}/health", ct);
            using var doc = JsonDocument.Parse(healthJson);
            health = doc.RootElement.GetProperty("status").GetString();
            version = doc.RootElement.GetProperty("serverVersion").GetString();
        }
        catch
        {
            // Server unreachable — service status below already communicates that.
        }

        return new ServerStatusSnapshot
        {
            ServiceStatus = GetServiceStatusText(ServiceName),
            PostgresServiceStatus = GetServiceStatusText(PgServiceName),
            ServerVersion = version,
            HealthStatus = health,
            LocalIpAddress = GetLocalIpAddress(),
            Port = port,
            StorageLocation = storageLocation,
            FreeDiskSpaceBytes = GetFreeDiskSpace(storageLocation)
        };
    }

    private static string GetServiceStatusText(string serviceName)
    {
        try
        {
            using var sc = new ServiceController(serviceName);
            return sc.Status switch
            {
                ServiceControllerStatus.Running => "Running",
                ServiceControllerStatus.Stopped => "Stopped",
                ServiceControllerStatus.StartPending => "Starting...",
                ServiceControllerStatus.StopPending => "Stopping...",
                _ => sc.Status.ToString()
            };
        }
        catch (InvalidOperationException)
        {
            return "Not installed";
        }
    }

    private int ReadPortFromConfig()
    {
        try
        {
            var path = Path.Combine(_installDir, "server", "appsettings.json");
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var bindAddress = doc.RootElement.GetProperty("Server").GetProperty("BindAddress").GetString() ?? "";
            var uri = new Uri(bindAddress.Replace("0.0.0.0", "localhost"));
            return uri.Port;
        }
        catch
        {
            return 5000;
        }
    }

    private string ReadStorageRootFromConfig()
    {
        try
        {
            var path = Path.Combine(_installDir, "server", "appsettings.json");
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            return doc.RootElement.GetProperty("Storage").GetProperty("StorageRoot").GetString()
                ?? @"C:\ProgramData\ResellerSystem\storage";
        }
        catch
        {
            return @"C:\ProgramData\ResellerSystem\storage";
        }
    }

    private static long GetFreeDiskSpace(string path)
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(path));
            if (string.IsNullOrEmpty(root)) return 0;
            return new DriveInfo(root).AvailableFreeSpace;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>First non-loopback IPv4 address — what to show the user as
    /// "other computers on your network can reach the server at ...".</summary>
    public static string GetLocalIpAddress()
    {
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;

                foreach (var addr in ni.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        return addr.Address.ToString();
                    }
                }
            }
        }
        catch { /* fall through to default */ }

        return "127.0.0.1";
    }
}
