using Npgsql;
using ResellerSystem.Server.Data.Configuration;

namespace ResellerSystem.Server.Data.Master;

public interface IMasterDatabaseHealthChecker
{
    Task<bool> IsHealthyAsync(CancellationToken ct = default);
}

public sealed class MasterDatabaseHealthChecker : IMasterDatabaseHealthChecker
{
    private readonly ConnectionStringFactory _connectionStringFactory;

    public MasterDatabaseHealthChecker(ConnectionStringFactory connectionStringFactory)
    {
        _connectionStringFactory = connectionStringFactory;
    }

    public async Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionStringFactory.BuildMasterConnectionString());
            await connection.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand("SELECT 1;", connection);
            await cmd.ExecuteScalarAsync(ct);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
