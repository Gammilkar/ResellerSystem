using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ResellerSystem.Domain.Shared.Dto;
using ResellerSystem.Server.Application.Backup;
using ResellerSystem.Server.Application.Update;
using ResellerSystem.Server.Application.VersionInfo;
using ResellerSystem.Server.FileStorage;
using ResellerSystem.Server.Modules.Abstractions;

namespace ResellerSystem.Server.Data.Update;

public sealed class UpdateService : IUpdateService
{
    private readonly UpdateOptions _options;
    private readonly IVersionProvider _versionProvider;
    private readonly IBackupService _backupService;
    private readonly IFileStorageService _fileStorage;
    private readonly HttpClient _httpClient;
    private readonly ILogger<UpdateService> _logger;

    public UpdateService(
        IOptions<UpdateOptions> options,
        IVersionProvider versionProvider,
        IBackupService backupService,
        IFileStorageService fileStorage,
        IHttpClientFactory httpClientFactory,
        ILogger<UpdateService> logger)
    {
        _options = options.Value;
        _versionProvider = versionProvider;
        _backupService = backupService;
        _fileStorage = fileStorage;
        _httpClient = httpClientFactory.CreateClient(nameof(UpdateService));
        _logger = logger;
    }

    public async Task<UpdateCheckResultDto> CheckForUpdateAsync(CancellationToken ct = default)
    {
        var manifest = await FetchManifestAsync(ct);
        var currentVersion = _versionProvider.ServerVersion;

        var hasUpdate = SemanticVersion.TryParse(manifest.ProductVersion, out var available)
            && SemanticVersion.TryParse(currentVersion, out var current)
            && available > current;

        return new UpdateCheckResultDto
        {
            UpdateAvailable = hasUpdate,
            CurrentVersion = currentVersion,
            AvailableVersion = hasUpdate ? manifest.ProductVersion : null,
            ReleaseNotesUrl = manifest.ReleaseNotesUrl
        };
    }

    public async Task<UpdateInstallResultDto> BeginInstallAsync(CancellationToken ct = default)
    {
        var manifest = await FetchManifestAsync(ct);
        if (!SemanticVersion.TryParse(manifest.ProductVersion, out var targetVersion) ||
            !SemanticVersion.TryParse(_versionProvider.ServerVersion, out var currentVersion) ||
            targetVersion <= currentVersion)
        {
            return new UpdateInstallResultDto { Status = UpdateInstallStatusDto.Failed, Message = "No newer version available." };
        }

        _logger.LogInformation("Beginning update install: {Current} -> {Target}", currentVersion, targetVersion);

        // 1. Download + verify checksum
        var packagePath = Path.Combine(_fileStorage.UpdateRoot, $"server-update-{manifest.ProductVersion}.zip");
        await DownloadAndVerifyAsync(manifest.Server, packagePath, ct);

        // 2. Mandatory backup — Update Engine never skips this (Product
        //    Development Plan v1.0, Part 2.3: "обязательный шаг, без опции пропустить").
        var backup = await _backupService.CreateFullBackupAsync(ct);
        _logger.LogInformation("Pre-update backup {BackupId} completed.", backup.Id);

        // 3. Hand off to the elevated, out-of-process updater. Server.Host
        //    cannot stop/replace/restart itself mid-request — see
        //    Server.Updater's own header comment for the full sequence it
        //    performs (stop -> extract -> swap junction -> start -> health
        //    check -> automatic FILE-level rollback on failure).
        //    KNOWN LIMITATION: on health-check failure, Server.Updater
        //    rolls back the file version but does NOT automatically restore
        //    the database backup — that is a manual "Restore" click in
        //    Server Manager against backup id below. See KNOWN_LIMITATIONS.md.
        LaunchElevatedUpdater(packagePath, manifest.ProductVersion, backup.Id);

        return new UpdateInstallResultDto
        {
            Status = UpdateInstallStatusDto.Applying,
            Message = "Update handed off to the updater process. The server will restart shortly — poll /health.",
            BackupId = backup.Id
        };
    }

    private async Task<UpdateManifestDto> FetchManifestAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.ManifestUrl))
        {
            throw new InvalidOperationException("Updates:ManifestUrl is not configured.");
        }

        var json = await _httpClient.GetStringAsync(_options.ManifestUrl, ct);
        return JsonSerializer.Deserialize<UpdateManifestDto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Update manifest could not be parsed.");
    }

    private async Task DownloadAndVerifyAsync(UpdatePackageDto package, string destinationPath, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

        await using (var httpStream = await _httpClient.GetStreamAsync(package.Url, ct))
        await using (var fileStream = File.Create(destinationPath))
        {
            await httpStream.CopyToAsync(fileStream, ct);
        }

        await using var verifyStream = File.OpenRead(destinationPath);
        var actualHash = Convert.ToHexString(await SHA256.HashDataAsync(verifyStream, ct)).ToLowerInvariant();

        if (!string.Equals(actualHash, package.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(destinationPath);
            throw new InvalidOperationException(
                $"Downloaded update package failed checksum verification (expected {package.Sha256}, got {actualHash}). Aborting install.");
        }
    }

    private void LaunchElevatedUpdater(string packagePath, string targetVersion, string backupId)
    {
        // Server.Host normally already runs elevated (Windows Service
        // account) so this typically needs no UAC prompt; when run via
        // `dotnet run` in dev it may, same per-action elevation pattern as
        // Desktop.ServerManager's ServiceControlHelper.
        var installDir = Directory.GetParent(AppContext.BaseDirectory)!.Parent!.FullName; // {app}\server-versions\{v}\ -> {app}
        var updaterExe = Path.Combine(installDir, "updater", "Server.Updater.exe");

        var psi = new ProcessStartInfo
        {
            FileName = updaterExe,
            Arguments = $"--install-dir \"{installDir}\" --package \"{packagePath}\" --version \"{targetVersion}\" " +
                        $"--backup-id \"{backupId}\" --health-timeout-seconds {_options.HealthCheckTimeoutSeconds}",
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden
        };

        Process.Start(psi);
        _logger.LogInformation("Launched Server.Updater for version {Version} (backup {BackupId}).", targetVersion, backupId);
    }
}
