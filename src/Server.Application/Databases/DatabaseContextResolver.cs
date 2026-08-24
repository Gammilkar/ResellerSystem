using ResellerSystem.Server.Application.Exceptions;
using ResellerSystem.Server.Domain.Abstractions;
using ResellerSystem.Server.Domain.Enums;

namespace ResellerSystem.Server.Application.Databases;

public sealed class DatabaseContextResolver : IDatabaseContextResolver
{
    private readonly IDatabaseProfileRepository _repository;
    private readonly ICurrentUserContext _currentUser;

    public DatabaseContextResolver(IDatabaseProfileRepository repository, ICurrentUserContext currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<ResolvedTenantContext> ResolveAsync(Guid databaseId, CancellationToken ct = default)
    {
        var profile = await _repository.GetByIdAsync(databaseId, ct)
            ?? throw new NotFoundException("DATABASE_NOT_FOUND", "Database was not found.");

        if (!_currentUser.CanAccessAllDatabases && !_currentUser.AllowedDatabaseIds.Contains(databaseId))
        {
            // Not reachable today (single local user has access to everything),
            // but keeps the enforcement point in place for when roles exist.
            throw new NotFoundException("DATABASE_NOT_FOUND", "Database was not found.");
        }

        if (profile.Status != DatabaseStatus.Ready || !profile.IsActive)
        {
            throw new DatabaseNotReadyException(
                $"Database '{profile.Name}' is not available (status: {profile.Status}, active: {profile.IsActive}).");
        }

        return new ResolvedTenantContext(profile.Id, profile.PhysicalDatabaseName, profile.Name);
    }
}
