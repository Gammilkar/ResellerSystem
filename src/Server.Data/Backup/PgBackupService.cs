using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ResellerSystem.Server.Application.Backup;
using ResellerSystem.Server.Application.Databases;
using ResellerSystem.Server.Application.Exceptions;
using ResellerSystem.Server.Application.VersionInfo;
using ResellerSystem.Server.Data.Configuration;
using ResellerSystem.Server.FileStorage;

namespace ResellerSystem.Server.Data.Backup;

/// <summary>
/// Shells out to pg_dump/pg_restore (bundled in the installer's
/// postgresql\bin — see PostgresOptions.BinDirectory) rather than
/// reimplementing PostgreSQL's binary dump format. This is the same
/// approach every serious PostgreSQL backup tool takes; pg_dump handles
/// schema+data consistently in one pass without needing us to reason about
/// FK ordering ourselves.
/// </summary>
public sealed class PgBackupService : IBackupService
{
    private readonly PostgresOptions _postgresOptions;
    private readonly ConnectionStringFactory _connectionStringFactory;
    private readonly IDatabaseProfileRepository _databaseProfileRepository;
    private readonly IFileStorageService _fileStorage;
    private readonly IVersionProvider _versionProvider;
    private readonly ILogger<PgBackupService> _logger;

    public PgBackupService(
        IOptions<PostgresOptions> postgresOptions,
        ConnectionStringFactory connectionStringFactory,
        IDatabaseProfileRepository databaseProfileRepository,
        IFileStorageService fileStorage,
        IVersionProvider versionProvider,
        ILogger<PgBackupService> logger)
    {
        _postgresOptions = postgresOptions.Value;
        _connectionStringFactory = connectionStringFactory;
        _databaseProfileRepository = databaseProfileRepository;
        _fileStorage = fileStorage;
        _versionProvider = versionProvider;
        _logger = logger;
    }

    public Task<BackupManifest> CreateDatabaseBackupAsync(CancellationToken ct = default) =>
        CreateBackupAsync(BackupType.Database, ct);

    public Task<BackupManifest> CreateFullBackupAsync(CancellationToken ct = default) =>
        CreateBackupAsync(BackupType.Full, ct);

    private async Task<BackupManifest> CreateBackupAsync(BackupType type, CancellationToken ct)
    {
        EnsureBinDirectoryConfigured();

        var backupId = $"{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..8]}";
        var backupFolder = Path.Combine(_fileStorage.BackupRoot, backupId);
        Directory.CreateDirectory(backupFolder);

        _logger.LogInformation("Starting {Type} backup {BackupId}", type, backupId);

        var entries = new List<BackupDatabaseEntry>();

        // Master DB first — smallest, and if this fails we want to know
        // before spending time dumping potentially-large tenant databases.
        entries.Add(await DumpDatabaseAsync(_postgresOptions.MasterDatabaseName, isMaster: true, backupFolder, ct));

        var tenants = await _databaseProfileRepository.GetAllAsync(ct);
        foreach (var tenant in tenants)
        {
            entries.Add(await DumpDatabaseAsync(tenant.PhysicalDatabaseName, isMaster: false, backupFolder, ct));
        }

        string? storageArchiveFileName = null;
        if (type == BackupType.Full)
        {
            storageArchiveFileName = "storage.zip";
            var archivePath = Path.Combine(backupFolder, storageArchiveFileName);
            if (Directory.Exists(_fileStorage.StorageRoot) && Directory.EnumerateFileSystemEntries(_fileStorage.StorageRoot).Any())
            {
                System.IO.Compression.ZipFile.CreateFromDirectory(_fileStorage.StorageRoot, archivePath);
            }
            else
            {
                // Nothing to archive yet (no documents module ships data
                // here at this stage) — write an empty zip so the manifest
                // entry is still meaningful and RestoreAsync has something
                // consistent to look for.
                using var emptyArchive = System.IO.Compression.ZipFile.Open(archivePath, System.IO.Compression.ZipArchiveMode.Create);
            }
        }

        var totalSize = entries.Sum(e => new FileInfo(Path.Combine(backupFolder, e.DumpFileName)).Length)
            + (storageArchiveFileName is not null ? new FileInfo(Path.Combine(backupFolder, storageArchiveFileName)).Length : 0);

        var manifest = new BackupManifest
        {
            Id = backupId,
            Type = type,
            CreatedAt = DateTimeOffset.UtcNow,
            ServerVersionAtBackup = _versionProvider.ServerVersion,
            Databases = entries,
            StorageArchiveFileName = storageArchiveFileName,
            TotalSizeBytes = totalSize
        };

        await File.WriteAllTextAsync(
            Path.Combine(backupFolder, "manifest.json"),
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }),
            ct);

        _logger.LogInformation("Completed {Type} backup {BackupId} ({SizeBytes} bytes, {DbCount} databases)",
            type, backupId, totalSize, entries.Count);

        return manifest;
    }

    private async Task<BackupDatabaseEntry> DumpDatabaseAsync(string physicalDatabaseName, bool isMaster, string backupFolder, CancellationToken ct)
    {
        var dumpFileName = $"{physicalDatabaseName}.dump";
        var dumpPath = Path.Combine(backupFolder, dumpFileName);
        var pgDumpExe = Path.Combine(_postgresOptions.BinDirectory, "pg_dump.exe");

        var connectionString = isMaster
            ? _connectionStringFactory.BuildMasterConnectionString()
            : _connectionStringFactory.BuildTenantConnectionString(physicalDatabaseName);

        var args = BuildPgToolArgs(connectionString, physicalDatabaseName)
            + $" --format=custom --file=\"{dumpPath}\"";

        await RunProcessAsync(pgDumpExe, args, ct, $"pg_dump ({physicalDatabaseName})");

        var checksum = await ComputeSha256Async(dumpPath, ct);
        return new BackupDatabaseEntry
        {
            PhysicalDatabaseName = physicalDatabaseName,
            DumpFileName = dumpFileName,
            Sha256Checksum = checksum,
            IsMaster = isMaster
        };
    }

    public async Task<IReadOnlyList<BackupManifest>> ListBackupsAsync(CancellationToken ct = default)
    {
        if (!Directory.Exists(_fileStorage.BackupRoot)) return Array.Empty<BackupManifest>();

        var results = new List<BackupManifest>();
        foreach (var dir in Directory.EnumerateDirectories(_fileStorage.BackupRoot))
        {
            var manifestPath = Path.Combine(dir, "manifest.json");
            if (!File.Exists(manifestPath)) continue;

            var manifest = JsonSerializer.Deserialize<BackupManifest>(await File.ReadAllTextAsync(manifestPath, ct));
            if (manifest is not null) results.Add(manifest);
        }

        return results.OrderByDescending(m => m.CreatedAt).ToList();
    }

    public async Task<BackupManifest> GetManifestAsync(string backupId, CancellationToken ct = default)
    {
        var manifestPath = Path.Combine(_fileStorage.BackupRoot, backupId, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            throw new NotFoundException("BACKUP_NOT_FOUND", $"Backup '{backupId}' was not found.");
        }

        return JsonSerializer.Deserialize<BackupManifest>(await File.ReadAllTextAsync(manifestPath, ct))
            ?? throw new NotFoundException("BACKUP_NOT_FOUND", $"Backup '{backupId}' manifest is unreadable.");
    }

    public async Task RestoreAsync(string backupId, CancellationToken ct = default)
    {
        EnsureBinDirectoryConfigured();

        var manifest = await GetManifestAsync(backupId, ct);
        var backupFolder = Path.Combine(_fileStorage.BackupRoot, backupId);

        _logger.LogWarning("Starting RESTORE from backup {BackupId} — this will overwrite current data.", backupId);

        foreach (var entry in manifest.Databases)
        {
            var dumpPath = Path.Combine(backupFolder, entry.DumpFileName);
            var actualChecksum = await ComputeSha256Async(dumpPath, ct);
            if (!string.Equals(actualChecksum, entry.Sha256Checksum, StringComparison.OrdinalIgnoreCase))
            {
                throw new ConflictException("BACKUP_CHECKSUM_MISMATCH",
                    $"Checksum mismatch for '{entry.DumpFileName}' — refusing to restore a possibly corrupted backup.");
            }

            await RestoreDatabaseAsync(entry, dumpPath, ct);
        }

        if (manifest.Type == BackupType.Full && manifest.StorageArchiveFileName is not null)
        {
            var archivePath = Path.Combine(backupFolder, manifest.StorageArchiveFileName);
            if (Directory.Exists(_fileStorage.StorageRoot))
            {
                Directory.Delete(_fileStorage.StorageRoot, recursive: true);
            }
            System.IO.Compression.ZipFile.ExtractToDirectory(archivePath, _fileStorage.StorageRoot);
        }

        _logger.LogWarning("Restore from backup {BackupId} completed.", backupId);
    }

    private async Task RestoreDatabaseAsync(BackupDatabaseEntry entry, string dumpPath, CancellationToken ct)
    {
        var pgRestoreExe = Path.Combine(_postgresOptions.BinDirectory, "pg_restore.exe");

        var connectionString = entry.IsMaster
            ? _connectionStringFactory.BuildMasterConnectionString()
            : _connectionStringFactory.BuildTenantConnectionString(entry.PhysicalDatabaseName);

        // --clean --if-exists drops existing objects first so a restore
        // onto an already-migrated (possibly newer-schema) database doesn't
        // collide with pre-existing tables.
        var args = BuildPgToolArgs(connectionString, entry.PhysicalDatabaseName)
            + $" --clean --if-exists \"{dumpPath}\"";

        await RunProcessAsync(pgRestoreExe, args, ct, $"pg_restore ({entry.PhysicalDatabaseName})");
    }

    private string BuildPgToolArgs(string connectionString, string databaseName)
    {
        // pg_dump/pg_restore read host/port/user from env vars (PGHOST etc.)
        // more reliably across versions than a --dbname connection-string
        // form with embedded password, so RunProcessAsync sets those instead
        // — see there. Here we just pass --dbname=<name>.
        return $"--dbname=\"{databaseName}\" --no-owner --no-privileges";
    }

    private async Task RunProcessAsync(string exePath, string arguments, CancellationToken ct, string description)
    {
        if (!File.Exists(exePath))
        {
            throw new InvalidOperationException(
                $"{description} failed: '{exePath}' not found. Postgres:BinDirectory is not configured correctly.");
        }

        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = arguments,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.Environment["PGHOST"] = _postgresOptions.Host;
        psi.Environment["PGPORT"] = _postgresOptions.Port.ToString();
        psi.Environment["PGUSER"] = _postgresOptions.AdminUsername;
        psi.Environment["PGPASSWORD"] = _postgresOptions.AdminPassword;

        using var process = Process.Start(psi) ?? throw new InvalidOperationException($"Failed to start {description}.");
        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            _logger.LogError("{Description} failed with exit code {ExitCode}: {Stderr}", description, process.ExitCode, stderr);
            throw new InvalidOperationException($"{description} failed (exit code {process.ExitCode}): {stderr}");
        }
    }

    private void EnsureBinDirectoryConfigured()
    {
        if (string.IsNullOrWhiteSpace(_postgresOptions.BinDirectory))
        {
            throw new InvalidOperationException(
                "Postgres:BinDirectory is not configured — backup/restore requires pg_dump/pg_restore " +
                "from the bundled PostgreSQL installation. This is set automatically by the Windows installer.");
        }
    }

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken ct)
    {
        await using var stream = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
