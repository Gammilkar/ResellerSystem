using Microsoft.EntityFrameworkCore;
using Npgsql;
using ResellerSystem.Server.Application.Databases;
using ResellerSystem.Server.Data.Configuration;
using ResellerSystem.Server.Data.Master;
using ResellerSystem.Server.Domain.Entities;

namespace ResellerSystem.Server.Data.Repositories;

public sealed class DatabaseProfileRepository : IDatabaseProfileRepository
{
    private readonly MasterDbContext _db;
    private readonly ConnectionStringFactory _connectionStringFactory;

    public DatabaseProfileRepository(MasterDbContext db, ConnectionStringFactory connectionStringFactory)
    {
        _db = db;
        _connectionStringFactory = connectionStringFactory;
    }

    public Task<DatabaseProfile?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.DatabaseProfiles.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<DatabaseProfile>> GetAllAsync(CancellationToken ct = default) =>
        await _db.DatabaseProfiles.OrderBy(p => p.CreatedAt).ToListAsync(ct);

    public Task<bool> PhysicalNameExistsAsync(string physicalDatabaseName, CancellationToken ct = default) =>
        _db.DatabaseProfiles.AnyAsync(p => p.PhysicalDatabaseName == physicalDatabaseName, ct);

    public async Task AddAsync(DatabaseProfile profile, CancellationToken ct = default)
    {
        _db.DatabaseProfiles.Add(profile);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(DatabaseProfile profile, CancellationToken ct = default)
    {
        _db.DatabaseProfiles.Update(profile);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Uses a PostgreSQL sequence in the master database (database_physical_seq)
    /// so physical name generation is atomic even with concurrent requests —
    /// never derived from, or dependent on, the user-supplied display name.
    /// </summary>
    public async Task<long> GetNextPhysicalSequenceAsync(CancellationToken ct = default)
    {
        var connectionString = _connectionStringFactory.BuildMasterConnectionString();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("SELECT nextval('database_physical_seq');", connection);
        var result = await cmd.ExecuteScalarAsync(ct);
        return (long)result!;
    }
}
