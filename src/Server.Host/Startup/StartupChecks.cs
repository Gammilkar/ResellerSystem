using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using ResellerSystem.Server.Application.Modules;
using ResellerSystem.Server.Application.Security;
using ResellerSystem.Server.Application.VersionInfo;
using ResellerSystem.Server.Data.Configuration;
using ResellerSystem.Server.Data.Master;
using ResellerSystem.Server.FileStorage;
using ResellerSystem.Server.Modules.Abstractions;

namespace ResellerSystem.Server.Host.Startup;

/// <summary>
/// Ordered startup sequence required by the architecture:
///   1. configuration validation
///   2. PostgreSQL reachability
///   3. master database existence + migrations
///   4. storage folders exist and are read/write-able
///   5. module registry — record "core" + every catalog module's version
///   6. initial admin account — auto-provisioned on first run only, so the
///      installer never requires a manual "create account" step
/// Any failure logs clearly and stops the host before it starts accepting
/// API traffic — a half-started server is worse than one that refuses to start.
/// </summary>
public static class StartupChecks
{
    public static async Task RunAsync(IServiceProvider services, ILogger logger, IReadOnlyList<IResellerModule> modules, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        logger.LogInformation("Startup check 1/6: validating configuration...");
        var postgresOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<PostgresOptions>>().Value;
        if (string.IsNullOrWhiteSpace(postgresOptions.Host) || string.IsNullOrWhiteSpace(postgresOptions.MasterDatabaseName))
        {
            throw new InvalidOperationException("Postgres configuration is incomplete. Check appsettings 'Postgres' section.");
        }
        logger.LogInformation("Configuration OK.");

        logger.LogInformation("Startup check 2/6: checking PostgreSQL connectivity...");
        var connectionStringFactory = sp.GetRequiredService<ConnectionStringFactory>();
        await using (var conn = new NpgsqlConnection(connectionStringFactory.BuildMaintenanceConnectionString()))
        {
            await conn.OpenAsync(ct);
        }
        logger.LogInformation("PostgreSQL is reachable at {Host}:{Port}.", postgresOptions.Host, postgresOptions.Port);

        logger.LogInformation("Startup check 3/6: ensuring master database and migrations...");
        var initializer = sp.GetRequiredService<MasterDatabaseInitializer>();
        var masterVersion = await initializer.InitializeAsync(ct);
        logger.LogInformation("Master database ready at schema version {Version}.", masterVersion);

        logger.LogInformation("Startup check 4/6: verifying file storage...");
        var fileStorage = sp.GetRequiredService<IFileStorageService>();
        fileStorage.EnsureRootsExist();
        var canReadWrite = await fileStorage.CheckReadWriteAsync(ct);
        if (!canReadWrite)
        {
            throw new InvalidOperationException($"Storage root '{fileStorage.StorageRoot}' is not readable/writable.");
        }
        logger.LogInformation("File storage OK. StorageRoot={StorageRoot}", fileStorage.StorageRoot);

        logger.LogInformation("Startup check 5/6: registering installed modules...");
        var moduleRegistry = sp.GetRequiredService<IModuleRegistry>();
        var versionProvider = sp.GetRequiredService<IVersionProvider>();

        await moduleRegistry.RegisterOrUpdateAsync(CoreModuleKey, versionProvider.ServerVersion, ct);
        logger.LogInformation("Registered core module at version {Version}.", versionProvider.ServerVersion);

        foreach (var module in modules)
        {
            await moduleRegistry.RegisterOrUpdateAsync(module.ModuleKey, module.Version, ct);
            logger.LogInformation("Registered module '{ModuleKey}' ({DisplayName}) at version {Version}.",
                module.ModuleKey, module.DisplayName, module.Version);
        }

        logger.LogInformation("Startup check 6/6: ensuring an admin account exists...");
        await EnsureInitialAdminAsync(sp, fileStorage, logger, ct);

        logger.LogInformation("All startup checks passed.");
    }

    /// <summary>
    /// First run only: creates a local admin account with a random
    /// generated password (never a hardcoded default — same principle as
    /// installer/scripts/Install-PostgreSql.ps1's PostgreSQL password) and
    /// writes it once to config/initial-admin-credentials.txt so the
    /// installer/Server Manager can show it to the user exactly once. This
    /// lets the Windows installer remain fully unattended (Product
    /// Development Plan v1.0: "пользователь не должен работать с
    /// PowerShell/CMD") while still requiring a real login.
    /// </summary>
    private static async Task EnsureInitialAdminAsync(IServiceProvider sp, IFileStorageService fileStorage, ILogger logger, CancellationToken ct)
    {
        var authService = sp.GetRequiredService<IAuthenticationService>();
        if (!await authService.NeedsInitialSetupAsync(ct))
        {
            logger.LogInformation("Admin account already exists — skipping initial provisioning.");
            return;
        }

        const string username = "admin";
        var password = GenerateRandomPassword();

        await authService.CreateInitialAdminAsync(username, password, ct);

        var configDir = Path.Combine(Path.GetDirectoryName(fileStorage.StorageRoot) ?? fileStorage.StorageRoot, "..", "config");
        configDir = Path.GetFullPath(configDir);
        Directory.CreateDirectory(configDir);

        var credentialsPath = Path.Combine(configDir, "initial-admin-credentials.txt");
        var payload = new
        {
            Username = username,
            Password = password,
            Note = "Change this password after first login. This file is only written once, on first server startup.",
            GeneratedAt = DateTimeOffset.UtcNow
        };
        await File.WriteAllTextAsync(credentialsPath, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }), ct);

        logger.LogWarning(
            "Created initial admin account '{Username}'. Credentials written to {Path} — " +
            "read them once from Server Manager and change the password after first login.",
            username, credentialsPath);
    }

    private static string GenerateRandomPassword()
    {
        const string allowedChars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%";
        var bytes = RandomNumberGenerator.GetBytes(24);
        var chars = new char[24];
        for (var i = 0; i < bytes.Length; i++)
        {
            chars[i] = allowedChars[bytes[i] % allowedChars.Length];
        }
        return new string(chars);
    }

    // Mirrors ResellerSystem.Server.Data.Migrations.SqlScriptMigrationRunner.CoreModuleKey —
    // duplicated as a literal rather than referenced directly so Server.Host
    // doesn't need a project reference to Server.Data purely for one constant
    // (it already gets Server.Data transitively via Server.Api for DI).
    private const string CoreModuleKey = "core";
}
