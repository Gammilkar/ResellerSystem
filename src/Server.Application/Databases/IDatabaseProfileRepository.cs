using ResellerSystem.Server.Domain.Entities;

namespace ResellerSystem.Server.Application.Databases;

/// <summary>
/// Persistence abstraction implemented by Server.Data. Kept in Server.Application
/// so business logic (DatabaseProvisioningService, resolvers) never depends on
/// EF Core or Npgsql directly.
/// </summary>
public interface IDatabaseProfileRepository
{
    Task<DatabaseProfile?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<DatabaseProfile>> GetAllAsync(CancellationToken ct = default);
    Task<bool> PhysicalNameExistsAsync(string physicalDatabaseName, CancellationToken ct = default);
    Task AddAsync(DatabaseProfile profile, CancellationToken ct = default);
    Task UpdateAsync(DatabaseProfile profile, CancellationToken ct = default);

    /// <summary>Next physical database sequence number, e.g. 15 -> "reseller_db_000015".</summary>
    Task<long> GetNextPhysicalSequenceAsync(CancellationToken ct = default);
}
