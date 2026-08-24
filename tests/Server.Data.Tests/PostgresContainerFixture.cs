using Testcontainers.PostgreSql;
using Xunit;

namespace ResellerSystem.Server.Data.Tests;

/// <summary>
/// Spins up a real, disposable PostgreSQL 16 container via Testcontainers for
/// each test class that needs one. Requires a working Docker daemon — these
/// are integration tests, not unit tests, and are skipped automatically by
/// most CI runners without Docker available (see README "Running tests").
/// </summary>
public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("postgres")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public string Host => _container.Hostname;
    public int Port => _container.GetMappedPublicPort(5432);

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition("Postgres collection")]
public sealed class PostgresCollection : ICollectionFixture<PostgresContainerFixture> { }
