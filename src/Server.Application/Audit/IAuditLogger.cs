namespace ResellerSystem.Server.Application.Audit;

/// <summary>Product Specification section 78 ("Audit Log"). One entry per
/// created/updated/deleted record (or per changed field, when tracking a
/// specific override) in the current tenant database — see
/// audit_log table.</summary>
public sealed record AuditEntry(
    string EntityType,
    Guid EntityId,
    string Action, // "Created" | "Updated" | "Deleted"
    string ChangedBy,
    string Source, // "manual" | "import" | "api" | "migration" | "system"
    string? FieldName = null,
    string? OldValue = null,
    string? NewValue = null);

public sealed record AuditLogEntry(
    Guid Id, string EntityType, Guid EntityId, string Action,
    string? FieldName, string? OldValue, string? NewValue,
    DateTimeOffset ChangedAt, string ChangedBy, string Source);

public interface IAuditLogger
{
    Task LogAsync(AuditEntry entry, CancellationToken ct = default);
    Task LogManyAsync(IReadOnlyList<AuditEntry> entries, CancellationToken ct = default);

    /// <summary>Most recent first. Pass entityType/entityId to see one
    /// record's history, or neither for a global recent-activity feed.</summary>
    Task<IReadOnlyList<AuditLogEntry>> ListAsync(string? entityType = null, Guid? entityId = null, int limit = 200, CancellationToken ct = default);
}
