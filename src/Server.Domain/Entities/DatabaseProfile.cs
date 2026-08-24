using ResellerSystem.Server.Domain.Enums;

namespace ResellerSystem.Server.Domain.Entities;

/// <summary>
/// A row in the MASTER database describing one tenant ("business") database.
///
/// IMPORTANT: <see cref="Id"/> is the immutable public identifier used by the
/// API and clients. <see cref="PhysicalDatabaseName"/> is the actual
/// PostgreSQL database name (e.g. "reseller_db_000015") and is NEVER derived
/// from the user-editable <see cref="Name"/>, and is never exposed outside
/// Server.Data/Server.Application. Renaming <see cref="Name"/> never touches
/// <see cref="PhysicalDatabaseName"/>.
/// </summary>
public sealed class DatabaseProfile : AuditableEntity
{
    public Guid Id { get; private set; }

    /// <summary>User-editable display name. Not guaranteed globally unique.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Physical PostgreSQL database name. Immutable after creation.
    /// Internal-only — never serialized to clients.
    /// </summary>
    public string PhysicalDatabaseName { get; private set; } = string.Empty;

    /// <summary>IANA time zone identifier (e.g. "America/Los_Angeles").</summary>
    public string TimeZone { get; set; } = "UTC";

    /// <summary>ISO 4217 currency code, e.g. "USD".</summary>
    public string Currency { get; set; } = "USD";

    public DatabaseStatus Status { get; private set; } = DatabaseStatus.Creating;

    public bool IsActive { get; set; } = true;

    /// <summary>Schema version applied to this tenant database (0 until first migration succeeds).</summary>
    public int SchemaVersion { get; private set; }

    private DatabaseProfile() { } // EF Core

    public static DatabaseProfile CreateNew(string name, string physicalDatabaseName, string timeZone, string currency)
    {
        if (string.IsNullOrWhiteSpace(physicalDatabaseName))
            throw new ArgumentException("Physical database name is required.", nameof(physicalDatabaseName));

        var now = DateTimeOffset.UtcNow;
        return new DatabaseProfile
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            PhysicalDatabaseName = physicalDatabaseName,
            TimeZone = timeZone,
            Currency = currency,
            Status = DatabaseStatus.Creating,
            IsActive = true,
            SchemaVersion = 0,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("Name cannot be empty.", nameof(newName));

        Name = newName.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ChangeTimeZone(string ianaTimeZone)
    {
        TimeZone = ianaTimeZone;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkReady(int schemaVersion)
    {
        Status = DatabaseStatus.Ready;
        SchemaVersion = schemaVersion;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkMigrationFailed()
    {
        Status = DatabaseStatus.MigrationFailed;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Disable()
    {
        Status = DatabaseStatus.Disabled;
        IsActive = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
