using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace ResellerSystem.Server.Data.Migrations;

public sealed partial class SqlScriptMigrationRunner : IMigrationRunner
{
    // Server.Data's own <RootNamespace> — see Server.Data.csproj. Embedded
    // resource logical names are generated from RootNamespace, NOT from the
    // assembly name, which can differ; every caller (including modules)
    // must state this explicitly rather than have the runner guess — a
    // previous version of this class guessed via Assembly.GetName().Name
    // and silently applied zero migrations because of the mismatch.
    private const string ServerDataRootNamespace = "ResellerSystem.Server.Data";
    public const string CoreModuleKey = "core";

    private const string EnsureMasterTrackingTableSql = """
        CREATE TABLE IF NOT EXISTS schema_migrations (
            version     INT PRIMARY KEY,
            script_name TEXT NOT NULL,
            applied_at  TIMESTAMPTZ NOT NULL
        );
        """;

    private const string EnsureTenantTrackingTableSql = """
        CREATE TABLE IF NOT EXISTS tenant_module_versions (
            module_key     TEXT NOT NULL,
            script_version INT NOT NULL,
            script_name    TEXT NOT NULL,
            applied_at     TIMESTAMPTZ NOT NULL,
            PRIMARY KEY (module_key, script_version)
        );
        """;

    private readonly ILogger<SqlScriptMigrationRunner> _logger;

    public SqlScriptMigrationRunner(ILogger<SqlScriptMigrationRunner> logger)
    {
        _logger = logger;
    }

    public async Task<int> ApplyMasterMigrationsAsync(string connectionString, CancellationToken ct = default)
    {
        var scripts = LoadEmbeddedScripts(Assembly.GetExecutingAssembly(), ServerDataRootNamespace, "Master");

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);

        await using (var ensureCmd = new NpgsqlCommand(EnsureMasterTrackingTableSql, connection))
        {
            await ensureCmd.ExecuteNonQueryAsync(ct);
        }

        var applied = await GetAppliedVersionsAsync(connection,
            "SELECT version FROM schema_migrations;", ct);
        var currentVersion = applied.Count == 0 ? 0 : applied.Max();

        foreach (var script in scripts.Where(s => !applied.Contains(s.Version)).OrderBy(s => s.Version))
        {
            _logger.LogInformation("Applying master migration {Version} ({ScriptName})", script.Version, script.Name);

            await using var transaction = await connection.BeginTransactionAsync(ct);
            try
            {
                await using (var cmd = new NpgsqlCommand(script.Sql, connection, transaction))
                {
                    await cmd.ExecuteNonQueryAsync(ct);
                }

                await using (var recordCmd = new NpgsqlCommand(
                    "INSERT INTO schema_migrations (version, script_name, applied_at) VALUES (@v, @n, now());",
                    connection, transaction))
                {
                    recordCmd.Parameters.AddWithValue("v", script.Version);
                    recordCmd.Parameters.AddWithValue("n", script.Name);
                    await recordCmd.ExecuteNonQueryAsync(ct);
                }

                await transaction.CommitAsync(ct);
                currentVersion = script.Version;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogError(ex, "Master migration {Version} ({ScriptName}) failed; rolled back", script.Version, script.Name);
                throw;
            }
        }

        return currentVersion;
    }

    public async Task<int> ApplyTenantModuleMigrationsAsync(ModuleMigrationTarget module, string connectionString, CancellationToken ct = default)
    {
        var scripts = LoadEmbeddedScripts(module.Assembly, module.RootNamespace, $"Tenant.{module.ModuleKey}");

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);

        await using (var ensureCmd = new NpgsqlCommand(EnsureTenantTrackingTableSql, connection))
        {
            await ensureCmd.ExecuteNonQueryAsync(ct);
        }

        var applied = await GetAppliedModuleVersionsAsync(connection, module.ModuleKey, ct);
        var currentVersion = applied.Count == 0 ? 0 : applied.Max();

        foreach (var script in scripts.Where(s => !applied.Contains(s.Version)).OrderBy(s => s.Version))
        {
            _logger.LogInformation("Applying tenant migration for module '{ModuleKey}' {Version} ({ScriptName})",
                module.ModuleKey, script.Version, script.Name);

            await using var transaction = await connection.BeginTransactionAsync(ct);
            try
            {
                await using (var cmd = new NpgsqlCommand(script.Sql, connection, transaction))
                {
                    await cmd.ExecuteNonQueryAsync(ct);
                }

                await using (var recordCmd = new NpgsqlCommand(
                    "INSERT INTO tenant_module_versions (module_key, script_version, script_name, applied_at) VALUES (@m, @v, @n, now());",
                    connection, transaction))
                {
                    recordCmd.Parameters.AddWithValue("m", module.ModuleKey);
                    recordCmd.Parameters.AddWithValue("v", script.Version);
                    recordCmd.Parameters.AddWithValue("n", script.Name);
                    await recordCmd.ExecuteNonQueryAsync(ct);
                }

                await transaction.CommitAsync(ct);
                currentVersion = script.Version;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogError(ex, "Tenant migration for module '{ModuleKey}' {Version} ({ScriptName}) failed; rolled back",
                    module.ModuleKey, script.Version, script.Name);
                throw;
            }
        }

        return currentVersion;
    }

    public async Task<IReadOnlyDictionary<string, int>> GetTenantModuleVersionsAsync(string connectionString, CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);

        await using var checkCmd = new NpgsqlCommand(
            "SELECT to_regclass('public.tenant_module_versions') IS NOT NULL;", connection);
        var exists = (bool)(await checkCmd.ExecuteScalarAsync(ct) ?? false);
        if (!exists) return new Dictionary<string, int>();

        var result = new Dictionary<string, int>();
        await using var cmd = new NpgsqlCommand(
            "SELECT module_key, MAX(script_version) FROM tenant_module_versions GROUP BY module_key;", connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result[reader.GetString(0)] = reader.GetInt32(1);
        }
        return result;
    }

    private static async Task<HashSet<int>> GetAppliedVersionsAsync(NpgsqlConnection connection, string sql, CancellationToken ct)
    {
        var result = new HashSet<int>();
        await using var cmd = new NpgsqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(reader.GetInt32(0));
        }
        return result;
    }

    private static async Task<HashSet<int>> GetAppliedModuleVersionsAsync(NpgsqlConnection connection, string moduleKey, CancellationToken ct)
    {
        var result = new HashSet<int>();
        await using var cmd = new NpgsqlCommand(
            "SELECT script_version FROM tenant_module_versions WHERE module_key = @m;", connection);
        cmd.Parameters.AddWithValue("m", moduleKey);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(reader.GetInt32(0));
        }
        return result;
    }

    /// <param name="folderPath">Dot-free path segments under
    /// "{rootNamespace}.Migrations.Scripts.", e.g. "Master" or
    /// "Tenant.inventory" — matches the physical folder structure
    /// Migrations/Scripts/Master/*.sql or Migrations/Scripts/Tenant/{key}/*.sql.</param>
    private static List<MigrationScript> LoadEmbeddedScripts(Assembly assembly, string rootNamespace, string folderPath)
    {
        var prefix = $"{rootNamespace}.Migrations.Scripts.{folderPath}.";

        var scripts = new List<MigrationScript>();
        foreach (var resourceName in assembly.GetManifestResourceNames().Where(n => n.StartsWith(prefix, StringComparison.Ordinal)))
        {
            var fileName = resourceName[prefix.Length..];
            var match = FileNameVersionRegex().Match(fileName);
            if (!match.Success) continue; // skip anything not matching NNNN_description.sql

            using var stream = assembly.GetManifestResourceStream(resourceName)!;
            using var reader = new StreamReader(stream);
            var sql = reader.ReadToEnd();

            scripts.Add(new MigrationScript(int.Parse(match.Groups[1].Value), fileName, sql));
        }

        // Unlike the old Core-only runner, we do NOT throw when a module has
        // zero scripts — a module with no schema needs (e.g. a pure
        // reporting/UI module) is legitimate. Callers that require at least
        // one script (Master, and the built-in "core" tenant module) check
        // for that themselves if needed.
        return scripts;
    }

    private sealed record MigrationScript(int Version, string Name, string Sql);

    [GeneratedRegex(@"^(\d{4})_.*\.sql$")]
    private static partial Regex FileNameVersionRegex();
}
