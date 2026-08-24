using Npgsql;
using ResellerSystem.Server.Data.Configuration;
using ResellerSystem.Server.Data.Migrations;

namespace ResellerSystem.Server.Data.Master;

/// <summary>
/// Startup-time bootstrap for the master database itself: creates
/// "reseller_system" if it doesn't exist yet (first run on a brand new
/// PostgreSQL instance) and applies master migrations. Called once from
/// Server.Host before the API starts accepting requests.
/// </summary>
public sealed class MasterDatabaseInitializer
{
    private readonly ConnectionStringFactory _connectionStringFactory;
    private readonly IMigrationRunner _migrationRunner;
    private readonly PostgresOptions _options;

    public MasterDatabaseInitializer(
        ConnectionStringFactory connectionStringFactory,
        IMigrationRunner migrationRunner,
        Microsoft.Extensions.Options.IOptions<PostgresOptions> options)
    {
        _connectionStringFactory = connectionStringFactory;
        _migrationRunner = migrationRunner;
        _options = options.Value;
    }

    public async Task<int> InitializeAsync(CancellationToken ct = default)
    {
        await EnsureMasterDatabaseExistsAsync(ct);
        var masterConnectionString = _connectionStringFactory.BuildMasterConnectionString();
        return await _migrationRunner.ApplyMasterMigrationsAsync(masterConnectionString, ct);
    }

    private async Task EnsureMasterDatabaseExistsAsync(CancellationToken ct)
    {
        var maintenanceConnectionString = _connectionStringFactory.BuildMaintenanceConnectionString();
        await using var connection = new NpgsqlConnection(maintenanceConnectionString);
        await connection.OpenAsync(ct);

        await using var checkCmd = new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname = @name;", connection);
        checkCmd.Parameters.AddWithValue("name", _options.MasterDatabaseName);
        var exists = await checkCmd.ExecuteScalarAsync(ct) is not null;
        if (exists) return;

        await using var createCmd = new NpgsqlCommand($"CREATE DATABASE \"{_options.MasterDatabaseName}\";", connection);
        await createCmd.ExecuteNonQueryAsync(ct);
    }
}
