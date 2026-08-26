using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NSubstitute;
using ResellerSystem.Domain.Shared.Dto;
using ResellerSystem.Modules.Inventory.Application;
using ResellerSystem.Modules.Inventory.Data;
using ResellerSystem.Server.Application.Audit;
using ResellerSystem.Server.Data.Migrations;
using ResellerSystem.Server.Domain.Abstractions;
using Xunit;

namespace ResellerSystem.Modules.Inventory.Tests;

/// <summary>
/// Regression coverage for a real bug found by the user while live-testing:
/// PurchaseService.CreateAsync threw a foreign-key violation
/// ("items_purchase_item_line_id_fkey") because InventoryDbContext never
/// told EF Core about the Item.PurchaseItemLineId -> PurchaseItemLine
/// relationship, so EF inserted Item rows before their parent
/// PurchaseItemLine row in the same SaveChangesAsync batch.
///
/// PurchaseServiceValidationTests is deliberately DB-free, and
/// PurchaseServiceSupplierPersistenceTests uses EF Core's InMemory provider
/// — neither can catch this class of bug, since InMemory does not enforce
/// foreign keys and DB-free tests never reach SaveChangesAsync at all. This
/// needs a real PostgreSQL database with the actual migrated schema (real
/// FK constraints) to fail the way the user's client actually failed.
/// </summary>
[Collection("Postgres collection")]
public class PurchaseServiceCreateAsyncPostgresTests
{
    private readonly PostgresContainerFixture _fixture;

    public PurchaseServiceCreateAsyncPostgresTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    private string BuildConnectionString(string database) =>
        $"Host={_fixture.Host};Port={_fixture.Port};Username=postgres;Password=postgres;Database={database}";

    private async Task<string> CreateMigratedTenantDatabaseAsync()
    {
        var dbName = $"test_inventory_{Guid.NewGuid():N}";
        await using (var conn = new NpgsqlConnection(BuildConnectionString("postgres")))
        {
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand($"CREATE DATABASE \"{dbName}\";", conn);
            await cmd.ExecuteNonQueryAsync();
        }

        var connectionString = BuildConnectionString(dbName);
        var runner = new SqlScriptMigrationRunner(NullLogger<SqlScriptMigrationRunner>.Instance);
        var target = new ModuleMigrationTarget("inventory", Assembly.GetAssembly(typeof(InventoryDbContext))!, "ResellerSystem.Modules.Inventory");
        await runner.ApplyTenantModuleMigrationsAsync(target, connectionString);

        return connectionString;
    }

    private sealed class FixedConnectionStringDbContextFactory : IInventoryDbContextFactory
    {
        private readonly string _connectionString;
        public FixedConnectionStringDbContextFactory(string connectionString) => _connectionString = connectionString;

        public InventoryDbContext CreateForCurrentTenant()
        {
            var options = new DbContextOptionsBuilder<InventoryDbContext>().UseNpgsql(_connectionString).Options;
            return new InventoryDbContext(options);
        }
    }

    [Fact]
    public async Task CreateAsync_persists_a_multi_line_multi_quantity_purchase_without_a_foreign_key_violation()
    {
        var connectionString = await CreateMigratedTenantDatabaseAsync();
        var dbContextFactory = new FixedConnectionStringDbContextFactory(connectionString);
        var auditLogger = Substitute.For<IAuditLogger>();
        var currentUser = Substitute.For<ICurrentUserContext>();
        currentUser.DisplayName.Returns("tester");
        var sut = new PurchaseService(dbContextFactory, auditLogger, currentUser);

        var request = new CreatePurchaseFullRequest
        {
            PurchaseDate = DateOnly.FromDateTime(DateTime.Today),
            SourceName = "Estate Sale",
            PurchaseType = "TaxPaid",
            ItemLines = new[]
            {
                new PurchaseItemLineInput { ItemName = "Пылесос Miele", Quantity = 1, UnitPurchaseCost = 24m },
                new PurchaseItemLineInput { ItemName = "Miele vacuum cleaner bags", Quantity = 3, UnitPurchaseCost = 4m }
            }
        };

        var act = async () => await sut.CreateAsync(request);

        var result = await act.Should().NotThrowAsync();
        result.Subject.ItemLines.Should().HaveCount(2);
        result.Subject.ItemLines.Sum(l => l.CreatedItems.Count).Should().Be(4);
        result.Subject.ItemLines.SelectMany(l => l.CreatedItems).Select(r => r.ItemNumber).Distinct().Should().HaveCount(4);
    }

    [Fact]
    public async Task CreateAsync_persists_the_new_descriptive_fields_onto_every_exploded_item()
    {
        var connectionString = await CreateMigratedTenantDatabaseAsync();
        var dbContextFactory = new FixedConnectionStringDbContextFactory(connectionString);
        var auditLogger = Substitute.For<IAuditLogger>();
        var currentUser = Substitute.For<ICurrentUserContext>();
        currentUser.DisplayName.Returns("tester");
        var sut = new PurchaseService(dbContextFactory, auditLogger, currentUser);

        var request = new CreatePurchaseFullRequest
        {
            PurchaseDate = DateOnly.FromDateTime(DateTime.Today),
            SourceName = "Estate Sale",
            PurchaseType = "TaxPaid",
            ItemLines = new[]
            {
                new PurchaseItemLineInput
                {
                    ItemName = "Miele Filter", Quantity = 2, UnitPurchaseCost = 10m,
                    Brand = "Miele", Model = "SF-AA50", SerialNumber = "SN-001",
                    SkuCustomLabel = "SKU-42", Condition = "Used", StorageLocation = "Garage A2"
                }
            }
        };

        var result = await sut.CreateAsync(request);

        await using var db = dbContextFactory.CreateForCurrentTenant();
        var itemIds = result.ItemLines.Single().CreatedItems.Select(r => r.Id).ToList();
        itemIds.Should().HaveCount(2);
        var items = await db.Items.Where(i => itemIds.Contains(i.Id)).ToListAsync();
        items.Should().HaveCount(2);
        foreach (var item in items)
        {
            item.Brand.Should().Be("Miele");
            item.Model.Should().Be("SF-AA50");
            item.SerialNumber.Should().Be("SN-001");
            item.SkuCustomLabel.Should().Be("SKU-42");
            item.Condition.Should().Be("Used");
            item.StorageLocation.Should().Be("Garage A2");
        }
    }

    [Fact]
    public async Task ToDetailDto_reflects_a_direct_Inventory_edit_to_an_items_category()
    {
        var connectionString = await CreateMigratedTenantDatabaseAsync();
        var dbContextFactory = new FixedConnectionStringDbContextFactory(connectionString);
        var auditLogger = Substitute.For<IAuditLogger>();
        var currentUser = Substitute.For<ICurrentUserContext>();
        currentUser.DisplayName.Returns("tester");
        var sut = new PurchaseService(dbContextFactory, auditLogger, currentUser);

        var created = await sut.CreateAsync(new CreatePurchaseFullRequest
        {
            PurchaseDate = DateOnly.FromDateTime(DateTime.Today),
            SourceName = "Estate Sale",
            PurchaseType = "TaxPaid",
            ItemLines = new[] { new PurchaseItemLineInput { ItemName = "Vacuum", CategoryName = "Original Category", Quantity = 1, UnitPurchaseCost = 10m } }
        });

        // Simulate a direct edit made through Inventory (not through the Purchase screen).
        await using (var db = dbContextFactory.CreateForCurrentTenant())
        {
            var itemId = created.ItemLines.Single().CreatedItems.Single().Id;
            var item = await db.Items.SingleAsync(i => i.Id == itemId);
            item.CategoryName = "Edited In Inventory";
            await db.SaveChangesAsync();
        }

        var reloaded = await sut.GetAsync(created.Id);

        reloaded.ItemLines.Single().CategoryName.Should().Be("Edited In Inventory");
    }
}
