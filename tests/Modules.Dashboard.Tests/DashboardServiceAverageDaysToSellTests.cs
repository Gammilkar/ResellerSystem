using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using NSubstitute;
using ResellerSystem.Modules.Dashboard.Application;
using ResellerSystem.Modules.Inventory.Data;
using ResellerSystem.Server.Application.Databases;
using ResellerSystem.Server.Data.Configuration;
using ResellerSystem.Server.Data.Migrations;
using Testcontainers.PostgreSql;
using Xunit;

namespace ResellerSystem.Modules.Dashboard.Tests;

/// <summary>
/// Regression coverage for a real bug the user found live: the Dashboard
/// showed "Average days to sell: -38" — an impossible negative value.
/// DashboardService.GetSalesAggregatesAsync used Item.CreatedAt (the row's
/// DB-insert timestamp) as a stand-in for "purchase date" instead of the
/// real Purchase.PurchaseDate. For imported/bulk-seeded data, CreatedAt
/// (when the row happened to be inserted into this database) can be weeks
/// after the real historical PurchaseDate, making "days to sell" negative.
/// Fixed to join Purchase.PurchaseDate properly. Uses a real Postgres
/// database (not EF InMemory) since this is testing a hand-written SQL
/// query, not an EF-mapped entity graph.
/// </summary>
public sealed class DashboardServiceAverageDaysToSellTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("postgres")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private string _connectionString = string.Empty;
    private string _dbName = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        _dbName = $"test_dashboard_{Guid.NewGuid():N}";
        var host = _container.Hostname;
        var port = _container.GetMappedPublicPort(5432);

        await using (var conn = new NpgsqlConnection($"Host={host};Port={port};Username=postgres;Password=postgres;Database=postgres"))
        {
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand($"CREATE DATABASE \"{_dbName}\";", conn);
            await cmd.ExecuteNonQueryAsync();
        }

        _connectionString = $"Host={host};Port={port};Username=postgres;Password=postgres;Database={_dbName}";

        var runner = new SqlScriptMigrationRunner(NullLogger<SqlScriptMigrationRunner>.Instance);
        await runner.ApplyTenantModuleMigrationsAsync(
            new ModuleMigrationTarget("inventory", Assembly.GetAssembly(typeof(InventoryDbContext))!, "ResellerSystem.Modules.Inventory"),
            _connectionString);
        await runner.ApplyTenantModuleMigrationsAsync(
            new ModuleMigrationTarget("sales", Assembly.GetAssembly(typeof(ResellerSystem.Modules.Sales.SalesModule))!, "ResellerSystem.Modules.Sales"),
            _connectionString);
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    [Fact]
    public async Task GetSummaryAsync_computes_average_days_to_sell_from_the_real_purchase_date_not_the_items_row_insert_timestamp()
    {
        var purchaseId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var purchaseDate = new DateOnly(2026, 6, 1);
        var saleDate = new DateOnly(2026, 6, 21); // 20 real days after the purchase
        // Deliberately insert the Item's audit CreatedAt far AFTER the real
        // purchase date — simulating a bulk import where rows land in the
        // DB weeks after the item was actually bought. If the bug were
        // still present (using created_at as "purchase date"), this would
        // produce a negative days-to-sell.
        var rowInsertedAt = DateTimeOffset.UtcNow;

        await using (var conn = new NpgsqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            await using (var cmd = new NpgsqlCommand("""
                INSERT INTO purchases (id, purchase_date, source_name, total_amount, sales_tax_amount, payment_method, used_reseller_permit, purchase_type, created_at, created_by, updated_at, updated_by)
                VALUES (@id, @purchaseDate, 'Estate Sale', 10, 0, NULL, false, 'TaxPaid', now(), 'tester', now(), 'tester');
                """, conn))
            {
                cmd.Parameters.AddWithValue("id", purchaseId);
                cmd.Parameters.AddWithValue("purchaseDate", purchaseDate);
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = new NpgsqlCommand("""
                INSERT INTO items (id, item_number, purchase_id, name, status, cost_basis_calculated, created_at, created_by, updated_at, updated_by)
                VALUES (@id, nextval('item_number_seq'), @purchaseId, 'Test Item', 'Sold', 10, @createdAt, 'tester', @createdAt, 'tester');
                """, conn))
            {
                cmd.Parameters.AddWithValue("id", itemId);
                cmd.Parameters.AddWithValue("purchaseId", purchaseId);
                cmd.Parameters.AddWithValue("createdAt", rowInsertedAt);
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = new NpgsqlCommand("""
                INSERT INTO sales (id, item_id, marketplace, sale_date, item_sale_price, gross_transaction_amount, payout_amount, created_at, updated_at)
                VALUES (@id, @itemId, 'eBay', @saleDate, 25, 25, 22, now(), now());
                """, conn))
            {
                cmd.Parameters.AddWithValue("id", Guid.NewGuid());
                cmd.Parameters.AddWithValue("itemId", itemId);
                cmd.Parameters.AddWithValue("saleDate", saleDate);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        var options = Options.Create(new PostgresOptions
        {
            Host = _container.Hostname,
            Port = _container.GetMappedPublicPort(5432),
            AdminUsername = "postgres",
            AdminPassword = "postgres"
        });
        var connectionStringFactory = new ConnectionStringFactory(options);
        var tenantAccessor = Substitute.For<ICurrentTenantAccessor>();
        tenantAccessor.Require().Returns(new ResolvedTenantContext(Guid.NewGuid(), _dbName, "Test Tenant"));
        var sut = new DashboardService(connectionStringFactory, tenantAccessor);

        var summary = await sut.GetSummaryAsync();

        summary.AverageDaysToSell.Should().Be(20);
    }
}
