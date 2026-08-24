using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ResellerSystem.Server.Data.Configuration;
using ResellerSystem.Server.Data.Migrations;
using ResellerSystem.Server.Data.Provisioning;
using ResellerSystem.Server.Modules.Abstractions;
using Xunit;

namespace ResellerSystem.Server.Data.Tests;

[Collection("Postgres collection")]
public class TenantDatabaseProvisionerTests
{
    private readonly PostgresContainerFixture _fixture;

    public TenantDatabaseProvisionerTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    private TenantDatabaseProvisioner CreateProvisioner()
    {
        var postgresOptions = Options.Create(new PostgresOptions
        {
            Host = _fixture.Host,
            Port = _fixture.Port,
            AdminUsername = "postgres",
            AdminPassword = "postgres",
            MasterDatabaseName = "postgres"
        });

        var connectionStringFactory = new ConnectionStringFactory(postgresOptions);
        var runner = new SqlScriptMigrationRunner(NullLogger<SqlScriptMigrationRunner>.Instance);
        // No business modules exist yet (Platform Refactor stage) — an empty
        // catalog exercises the "core module only" path.
        var emptyCatalog = new StaticServerModuleCatalog(Array.Empty<IResellerModule>());
        return new TenantDatabaseProvisioner(connectionStringFactory, runner, emptyCatalog);
    }

    [Fact]
    public async Task CreateDatabaseAsync_then_ApplyTenantMigrationsAsync_results_in_Ready_schema()
    {
        var provisioner = CreateProvisioner();
        var physicalName = $"reseller_db_test_{Guid.NewGuid():N}"[..40];

        (await provisioner.DatabaseExistsAsync(physicalName)).Should().BeFalse();

        await provisioner.CreateDatabaseAsync(physicalName);
        (await provisioner.DatabaseExistsAsync(physicalName)).Should().BeTrue();

        var schemaVersion = await provisioner.ApplyTenantMigrationsAsync(physicalName);
        schemaVersion.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task CreateDatabaseAsync_refuses_unsafe_physical_names()
    {
        var provisioner = CreateProvisioner();

        var act = async () => await provisioner.CreateDatabaseAsync("Robert'); DROP TABLE database_profiles;--");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
