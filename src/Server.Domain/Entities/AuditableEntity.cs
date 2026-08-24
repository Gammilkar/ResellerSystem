namespace ResellerSystem.Server.Domain.Entities;

/// <summary>
/// Base for entities that need creation/modification tracking.
/// CreatedBy/UpdatedBy are string identifiers (not FKs to a User table yet) —
/// see ICurrentUserContext. Kept nullable-free with sensible defaults so a
/// single-user system today doesn't require special-casing.
/// </summary>
public abstract class AuditableEntity
{
    public DateTimeOffset CreatedAt { get; set; }
    public string CreatedBy { get; set; } = "system";
    public DateTimeOffset UpdatedAt { get; set; }
    public string UpdatedBy { get; set; } = "system";
}
