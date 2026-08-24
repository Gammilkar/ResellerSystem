namespace ResellerSystem.Server.Domain.Enums;

/// <summary>
/// Lifecycle of a tenant database as tracked in the master database.
/// A tenant is only considered usable once it reaches Ready.
/// </summary>
public enum DatabaseStatus
{
    /// <summary>Physical DB created, migrations in progress.</summary>
    Creating = 0,

    /// <summary>Migrations applied successfully, safe to use.</summary>
    Ready = 1,

    /// <summary>Provisioning or a later migration failed — never silently treated as Ready.</summary>
    MigrationFailed = 2,

    /// <summary>Administratively disabled; not selectable by clients.</summary>
    Disabled = 3
}
