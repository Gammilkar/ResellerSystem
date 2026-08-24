namespace ResellerSystem.Server.Data.Configuration;

/// <summary>
/// Connection settings for the PostgreSQL server instance (not a specific
/// database). Master and tenant connection strings are built from this at
/// runtime by Server.Data — never stored pre-assembled with a tenant name.
/// Password comes from configuration/environment/user-secrets, never from
/// source control (see appsettings.json placeholder + README).
/// </summary>
public sealed class PostgresOptions
{
    public const string SectionName = "Postgres";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5432;
    public string AdminUsername { get; set; } = "postgres";
    public string AdminPassword { get; set; } = string.Empty;
    public string MasterDatabaseName { get; set; } = "reseller_system";

    /// <summary>Absolute path to the PostgreSQL "bin" folder (pg_dump.exe,
    /// pg_restore.exe) — set by the installer to "{InstallDir}\postgresql\bin"
    /// (see installer/scripts/Install-PostgreSql.ps1). Empty in dev unless
    /// docker-compose PostgreSQL's matching client tools are installed
    /// locally and this is pointed at them.</summary>
    public string BinDirectory { get; set; } = string.Empty;
}
