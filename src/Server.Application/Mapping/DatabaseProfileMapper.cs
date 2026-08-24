using ResellerSystem.Domain.Shared.Dto;
using ResellerSystem.Domain.Shared.Enums;
using ResellerSystem.Server.Domain.Entities;
using ResellerSystem.Server.Domain.Enums;

namespace ResellerSystem.Server.Application.Mapping;

public static class DatabaseProfileMapper
{
    public static DatabaseProfileDto ToDto(this DatabaseProfile entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        TimeZone = entity.TimeZone,
        Currency = entity.Currency,
        Status = MapStatus(entity.Status),
        IsActive = entity.IsActive,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt
    };

    private static DatabaseStatusDto MapStatus(DatabaseStatus status) => status switch
    {
        DatabaseStatus.Creating => DatabaseStatusDto.Creating,
        DatabaseStatus.Ready => DatabaseStatusDto.Ready,
        DatabaseStatus.MigrationFailed => DatabaseStatusDto.MigrationFailed,
        DatabaseStatus.Disabled => DatabaseStatusDto.Disabled,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };
}
