using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ResellerSystem.Server.Data.Configuration;
using ResellerSystem.Server.Data.Migrations;
using ResellerSystem.Server.Data.Modules;
using Xunit;

namespace ResellerSystem.Server.Data.Tests;

[Collection("Postgres collection")]
public class ModuleRegistryTests
{
    private readonly PostgresContainerFixture _fixture;

    public ModuleRegistryTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<(ModuleRegistry Registry, string DbName)> CreatePreparedRegistryAsync()
    {
        var dbName = $"test_moduleregistry_{Guid.NewGuid():N}";

        var options = Options.Create(new PostgresOptions
        {
            Host = _fixture.Host,
            Port = _fixture.Port,
            AdminUsername = "postgres",
            AdminPassword = "postgres",
            MasterDatabaseName = dbName
        });

        var connectionStringFactory = new ConnectionStringFactory(options);

        await using (var conn = new Npgsql.NpgsqlConnection(
            $"Host={_fixture.Host};Port={_fixture.Port};Username=postgres;Password=postgres;Database=postgres"))
        {
            await conn.OpenAsync();
            await using var cmd = new Npgsql.NpgsqlCommand($"CREATE DATABASE \"{dbName}\";", conn);
            await cmd.ExecuteNonQueryAsync();
        }

        var runner = new SqlScriptMigrationRunner(NullLogger<SqlScriptMigrationRunner>.Instance);
        await runner.ApplyMasterMigrationsAsync(connectionStringFactory.BuildMasterConnectionString());

        return (new ModuleRegistry(connectionStringFactory), dbName);
    }

    [Fact]
    public async Task RegisterOrUpdateAsync_then_GetInstalledModulesAsync_roundtrips()
    {
        var (registry, _) = await CreatePreparedRegistryAsync();

        await registry.RegisterOrUpdateAsync("core", "0.1.0");

        var modules = await registry.GetInstalledModulesAsync();

        modules.Should().ContainSingle(m => m.ModuleKey == "core" && m.Version == "0.1.0");
    }

    [Fact]
    public async Task RegisterOrUpdateAsync_upserts_existing_module_version()
    {
        var (registry, _) = await CreatePreparedRegistryAsync();

        await registry.RegisterOrUpdateAsync("core", "0.1.0");
        await registry.RegisterOrUpdateAsync("core", "0.2.0");

        var modules = await registry.GetInstalledModulesAsync();

        modules.Should().ContainSingle(m => m.ModuleKey == "core");
        modules.Single(m => m.ModuleKey == "core").Version.Should().Be("0.2.0");
    }
}
