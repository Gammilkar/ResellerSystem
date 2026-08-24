using Microsoft.Extensions.Options;
using Npgsql;

namespace ResellerSystem.Server.Data.Configuration;

/// <summary>
/// Only place in the codebase that assembles a PostgreSQL connection string.
/// Credentials never leave this project — Server.Api/Desktop clients only
/// ever see a Database Id (Guid), never a connection string or password.
/// </summary>
public sealed class ConnectionStringFactory
{
    private readonly PostgresOptions _options;

    public ConnectionStringFactory(IOptions<PostgresOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>Connection string to the "postgres" maintenance database — used only for CREATE DATABASE.</summary>
    public string BuildMaintenanceConnectionString() => Build("postgres");

    public string BuildMasterConnectionString() => Build(_options.MasterDatabaseName);

    public string BuildTenantConnectionString(string physicalDatabaseName) => Build(physicalDatabaseName);

    private string Build(string databaseName)
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = _options.Host,
            Port = _options.Port,
            Username = _options.AdminUsername,
            Password = _options.AdminPassword,
            Database = databaseName,
            Pooling = true
        };
        return builder.ConnectionString;
    }
}
