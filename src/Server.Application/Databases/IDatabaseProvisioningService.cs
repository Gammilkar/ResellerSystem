using ResellerSystem.Domain.Shared.Dto;

namespace ResellerSystem.Server.Application.Databases;

public interface IDatabaseProvisioningService
{
    Task<DatabaseProfileDto> CreateAsync(CreateDatabaseRequest request, CancellationToken ct = default);
    Task<DatabaseProfileDto> UpdateAsync(Guid id, UpdateDatabaseRequest request, CancellationToken ct = default);
    Task<DatabaseProfileDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<DatabaseProfileDto>> ListAsync(CancellationToken ct = default);
}
