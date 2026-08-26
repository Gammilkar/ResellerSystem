using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using ResellerSystem.Domain.Shared.Dto;
using ResellerSystem.Modules.Inventory.Application;
using ResellerSystem.Modules.Inventory.Data;
using ResellerSystem.Modules.Inventory.Domain;
using ResellerSystem.Server.Application.Audit;
using ResellerSystem.Server.Domain.Abstractions;
using Xunit;

namespace ResellerSystem.Modules.Inventory.Tests;

/// <summary>
/// Regression coverage for a real bug found during self-review: SupplierId
/// was applied to the Purchase entity in UpdateAsync but never in
/// ApplyFullWorkflowFields (shared by CreateAsync/UpdateAsync), so linking a
/// Supplier via the Desktop client's new "Тип источника" picker silently
/// failed to persist on a brand-new Purchase — it only "worked" on the next
/// Update. Unlike PurchaseServiceValidationTests (deliberately DB-free, see
/// its doc-comment), this needs an actual DbContext round-trip to catch
/// this class of bug, so it uses the EF Core InMemory provider — acceptable
/// here since the behavior under test (a plain scalar property assignment
/// and a FirstOrDefaultAsync lookup) has no Postgres-specific semantics.
/// </summary>
public class PurchaseServiceSupplierPersistenceTests
{
    private readonly IInventoryDbContextFactory _dbContextFactory = Substitute.For<IInventoryDbContextFactory>();
    private readonly IAuditLogger _auditLogger = Substitute.For<IAuditLogger>();
    private readonly ICurrentUserContext _currentUser = Substitute.For<ICurrentUserContext>();
    private readonly PurchaseService _sut;
    private readonly string _dbName = Guid.NewGuid().ToString();

    public PurchaseServiceSupplierPersistenceTests()
    {
        _sut = new PurchaseService(_dbContextFactory, _auditLogger, _currentUser);
        _dbContextFactory.CreateForCurrentTenant().Returns(_ => NewContext());
        _currentUser.DisplayName.Returns("tester");
    }

    private InventoryDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>().UseInMemoryDatabase(_dbName).Options;
        return new InventoryDbContext(options);
    }

    private async Task<Supplier> SeedSupplierAsync()
    {
        var supplier = Supplier.CreateNew("Acme Trading", null, null, null, null);
        await using var db = NewContext();
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();
        return supplier;
    }

    [Fact]
    public async Task CreateAsync_persists_SupplierId_on_a_brand_new_purchase()
    {
        var supplier = await SeedSupplierAsync();
        var request = MakeRequest(supplier.Id);

        var created = await _sut.CreateAsync(request);

        created.SupplierId.Should().Be(supplier.Id);

        await using var db = NewContext();
        var stored = await db.Purchases.SingleAsync(p => p.Id == created.Id);
        stored.SupplierId.Should().Be(supplier.Id);
    }

    [Fact]
    public async Task UpdateAsync_persists_SupplierId_when_it_is_set_for_the_first_time()
    {
        var created = await _sut.CreateAsync(MakeRequest(supplierId: null));
        var supplier = await SeedSupplierAsync();

        var updateRequest = MakeRequest(supplier.Id);
        var updated = await _sut.UpdateAsync(created.Id, new UpdatePurchaseFullRequest
        {
            PurchaseDate = updateRequest.PurchaseDate,
            SourceName = updateRequest.SourceName,
            SupplierId = updateRequest.SupplierId,
            PurchaseType = updateRequest.PurchaseType,
            ItemLines = updateRequest.ItemLines,
            ExpenseLines = updateRequest.ExpenseLines
        });

        updated.SupplierId.Should().Be(supplier.Id);

        await using var db = NewContext();
        var stored = await db.Purchases.SingleAsync(p => p.Id == created.Id);
        stored.SupplierId.Should().Be(supplier.Id);
    }

    private static CreatePurchaseFullRequest MakeRequest(Guid? supplierId) => new()
    {
        PurchaseDate = DateOnly.FromDateTime(DateTime.Today),
        SourceName = "Estate Sale",
        SupplierId = supplierId,
        PurchaseType = "TaxPaid",
        ItemLines = new[] { new PurchaseItemLineInput { ItemName = "Widget", Quantity = 1, UnitPurchaseCost = 10m } },
        ExpenseLines = Array.Empty<PurchaseExpenseLineInput>()
    };
}
