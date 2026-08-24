namespace ResellerSystem.Domain.Shared.Enums;

/// <summary>
/// Public/API-facing lifecycle status of a tenant database.
/// Mirrors Server.Domain.Enums.DatabaseStatus but is intentionally a separate
/// type so the wire contract can evolve independently from the domain model.
/// </summary>
public enum DatabaseStatusDto
{
    Creating = 0,
    Ready = 1,
    MigrationFailed = 2,
    Disabled = 3
}
