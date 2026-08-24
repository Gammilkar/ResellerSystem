using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using ResellerSystem.Server.Data.Migrations;
using Xunit;

namespace ResellerSystem.Server.Data.Tests;

[Collection("Postgres collection")]
public class SqlScriptMigrationRunnerTests
{
    private const string ServerDataRootNamespace = "ResellerSystem.Server.Data";

    private readonly PostgresContainerFixture _fixture;

    public SqlScriptMigrationRunnerTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    private string BuildConnectionString(string database) =>
        $"Host={_fixture.Host};Port={_fixture.Port};Username=postgres;Password=postgres;Database={database}";

    private async Task CreateDatabaseAsync(string name)
    {
        await using var conn = new NpgsqlConnection(BuildConnectionString("postgres"));
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand($"CREATE DATABASE \"{name}\";", conn);
        await cmd.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task Applying_master_migrations_creates_database_profiles_and_installed_modules_tables()
    {
        var dbName = $"test_master_{Guid.NewGuid():N}";
        await CreateDatabaseAsync(dbName);
        var runner = new SqlScriptMigrationRunner(NullLogger<SqlScriptMigrationRunner>.Instance);

        var version = await runner.ApplyMasterMigrationsAsync(BuildConnectionString(dbName));

        version.Should().BeGreaterThanOrEqualTo(2); // 0001_init + 0002_installed_modules

        await using var conn = new NpgsqlConnection(BuildConnectionString(dbName));
        await conn.OpenAsync();

        await using (var cmd = new NpgsqlCommand("SELECT to_regclass('public.database_profiles') IS NOT NULL;", conn))
        {
            ((bool)(await cmd.ExecuteScalarAsync())!).Should().BeTrue();
        }

        await using (var cmd = new NpgsqlCommand("SELECT to_regclass('public.installed_modules') IS NOT NULL;", conn))
        {
            ((bool)(await cmd.ExecuteScalarAsync())!).Should().BeTrue();
        }
    }

    [Fact]
    public async Task Applying_core_tenant_module_migrations_creates_tenant_info_table()
    {
        var dbName = $"test_tenant_{Guid.NewGuid():N}";
        await CreateDatabaseAsync(dbName);
        var runner = new SqlScriptMigrationRunner(NullLogger<SqlScriptMigrationRunner>.Instance);

        var target = new ModuleMigrationTarget(SqlScriptMigrationRunner.CoreModuleKey, Assembly.GetAssembly(typeof(SqlScriptMigrationRunner))!, ServerDataRootNamespace);
        var version = await runner.ApplyTenantModuleMigrationsAsync(target, BuildConnectionString(dbName));

        version.Should().BeGreaterThanOrEqualTo(1);

        await using var conn = new NpgsqlConnection(BuildConnectionString(dbName));
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT to_regclass('public.tenant_info') IS NOT NULL;", conn);
        var exists = (bool)(await cmd.ExecuteScalarAsync())!;
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task Applying_migrations_twice_is_idempotent()
    {
        var dbName = $"test_idempotent_{Guid.NewGuid():N}";
        await CreateDatabaseAsync(dbName);
        var runner = new SqlScriptMigrationRunner(NullLogger<SqlScriptMigrationRunner>.Instance);

        var firstVersion = await runner.ApplyMasterMigrationsAsync(BuildConnectionString(dbName));
        var secondVersion = await runner.ApplyMasterMigrationsAsync(BuildConnectionString(dbName));

        secondVersion.Should().Be(firstVersion);
    }

    [Fact]
    public async Task GetTenantModuleVersionsAsync_returns_empty_for_unmigrated_database()
    {
        var dbName = $"test_unmigrated_{Guid.NewGuid():N}";
        await CreateDatabaseAsync(dbName);
        var runner = new SqlScriptMigrationRunner(NullLogger<SqlScriptMigrationRunner>.Instance);

        var versions = await runner.GetTenantModuleVersionsAsync(BuildConnectionString(dbName));

        versions.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTenantModuleVersionsAsync_reports_version_per_module()
    {
        var dbName = $"test_moduleversions_{Guid.NewGuid():N}";
        await CreateDatabaseAsync(dbName);
        var runner = new SqlScriptMigrationRunner(NullLogger<SqlScriptMigrationRunner>.Instance);

        var target = new ModuleMigrationTarget(SqlScriptMigrationRunner.CoreModuleKey, Assembly.GetAssembly(typeof(SqlScriptMigrationRunner))!, ServerDataRootNamespace);
        await runner.ApplyTenantModuleMigrationsAsync(target, BuildConnectionString(dbName));

        var versions = await runner.GetTenantModuleVersionsAsync(BuildConnectionString(dbName));

        versions.Should().ContainKey(SqlScriptMigrationRunner.CoreModuleKey);
        versions[SqlScriptMigrationRunner.CoreModuleKey].Should().BeGreaterThanOrEqualTo(1);
    }
}
