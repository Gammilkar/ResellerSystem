using Npgsql;
using ResellerSystem.Server.Application.Audit;
using ResellerSystem.Server.Application.Databases;
using ResellerSystem.Server.Data.Configuration;

namespace ResellerSystem.Server.Data.Audit;

/// <summary>Raw-SQL insert against the current tenant's audit_log table —
/// same pattern as SessionService (master DB) and ReportsService/
/// DashboardService (tenant DB reads).</summary>
public sealed class AuditLogger : IAuditLogger
{
    private readonly ConnectionStringFactory _connectionStringFactory;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public AuditLogger(ConnectionStringFactory connectionStringFactory, ICurrentTenantAccessor tenantAccessor)
    {
        _connectionStringFactory = connectionStringFactory;
        _tenantAccessor = tenantAccessor;
    }

    public async Task LogAsync(AuditEntry entry, CancellationToken ct = default)
    {
        var tenant = _tenantAccessor.Require();
        var connectionString = _connectionStringFactory.BuildTenantConnectionString(tenant.PhysicalDatabaseName);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await InsertAsync(connection, null, entry, ct);
    }

    public async Task LogManyAsync(IReadOnlyList<AuditEntry> entries, CancellationToken ct = default)
    {
        if (entries.Count == 0) return;

        var tenant = _tenantAccessor.Require();
        var connectionString = _connectionStringFactory.BuildTenantConnectionString(tenant.PhysicalDatabaseName);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        foreach (var entry in entries)
        {
            await InsertAsync(connection, transaction, entry, ct);
        }

        await transaction.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<AuditLogEntry>> ListAsync(string? entityType = null, Guid? entityId = null, int limit = 200, CancellationToken ct = default)
    {
        var tenant = _tenantAccessor.Require();
        var connectionString = _connectionStringFactory.BuildTenantConnectionString(tenant.PhysicalDatabaseName);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);

        var sql = """
            SELECT id, entity_type, entity_id, action, field_name, old_value, new_value, changed_at, changed_by, source
            FROM audit_log
            WHERE (@et::text IS NULL OR entity_type = @et)
              AND (@eid::uuid IS NULL OR entity_id = @eid)
            ORDER BY changed_at DESC
            LIMIT @lim;
            """;

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("et", (object?)entityType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("eid", (object?)entityId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("lim", limit);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<AuditLogEntry>();
        while (await reader.ReadAsync(ct))
        {
            results.Add(new AuditLogEntry(
                reader.GetGuid(0), reader.GetString(1), reader.GetGuid(2), reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.GetFieldValue<DateTimeOffset>(7), reader.GetString(8), reader.GetString(9)));
        }
        return results;
    }

    private static async Task InsertAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, AuditEntry entry, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO audit_log (id, entity_type, entity_id, action, field_name, old_value, new_value, changed_at, changed_by, source)
            VALUES (@id, @et, @eid, @act, @fn, @ov, @nv, now(), @by, @src);
            """;

        await using var cmd = transaction is null
            ? new NpgsqlCommand(sql, connection)
            : new NpgsqlCommand(sql, connection, transaction);

        cmd.Parameters.AddWithValue("id", Guid.NewGuid());
        cmd.Parameters.AddWithValue("et", entry.EntityType);
        cmd.Parameters.AddWithValue("eid", entry.EntityId);
        cmd.Parameters.AddWithValue("act", entry.Action);
        cmd.Parameters.AddWithValue("fn", (object?)entry.FieldName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("ov", (object?)entry.OldValue ?? DBNull.Value);
        cmd.Parameters.AddWithValue("nv", (object?)entry.NewValue ?? DBNull.Value);
        cmd.Parameters.AddWithValue("by", entry.ChangedBy);
        cmd.Parameters.AddWithValue("src", entry.Source);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
