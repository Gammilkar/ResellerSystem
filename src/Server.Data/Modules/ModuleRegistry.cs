using Npgsql;
using ResellerSystem.Server.Application.Modules;
using ResellerSystem.Server.Data.Configuration;

namespace ResellerSystem.Server.Data.Modules;

/// <summary>
/// Raw-SQL implementation against the master database's `installed_modules`
/// table (see Migrations/Scripts/Master/0002_installed_modules.sql).
/// Deliberately not EF-mapped — this is a two-column upsert table, adding a
/// full entity + DbContext mapping for it would be unnecessary ceremony.
/// </summary>
public sealed class ModuleRegistry : IModuleRegistry
{
    private readonly ConnectionStringFactory _connectionStringFactory;

    public ModuleRegistry(ConnectionStringFactory connectionStringFactory)
    {
        _connectionStringFactory = connectionStringFactory;
    }

    public async Task<IReadOnlyList<InstalledModuleInfo>> GetInstalledModulesAsync(CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(_connectionStringFactory.BuildMasterConnectionString());
        await connection.OpenAsync(ct);

        var result = new List<InstalledModuleInfo>();
        await using var cmd = new NpgsqlCommand(
            "SELECT module_key, version, installed_at, updated_at FROM installed_modules ORDER BY module_key;",
            connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new InstalledModuleInfo(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetFieldValue<DateTimeOffset>(2),
                reader.GetFieldValue<DateTimeOffset>(3)));
        }
        return result;
    }

    public async Task RegisterOrUpdateAsync(string moduleKey, string version, CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(_connectionStringFactory.BuildMasterConnectionString());
        await connection.OpenAsync(ct);

        const string sql = """
            INSERT INTO installed_modules (module_key, version, installed_at, updated_at)
            VALUES (@key, @version, now(), now())
            ON CONFLICT (module_key)
            DO UPDATE SET version = @version, updated_at = now();
            """;

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("key", moduleKey);
        cmd.Parameters.AddWithValue("version", version);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
