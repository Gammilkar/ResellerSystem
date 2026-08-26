using Testcontainers.PostgreSql;
using Xunit;

namespace ResellerSystem.Modules.Inventory.Tests;

/// <summary>
/// Same pattern as Server.Data.Tests.PostgresContainerFixture — a real,
/// disposable PostgreSQL 16 container for integration tests that need real
/// FK/constraint enforcement, which EF Core's InMemory provider (used
/// elsewhere in this project, e.g. PurchaseServiceSupplierPersistenceTests)
/// silently does not enforce. Requires a working Docker daemon.
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
