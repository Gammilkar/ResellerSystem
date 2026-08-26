using FluentAssertions;
using NSubstitute;
using ResellerSystem.Domain.Shared.Dto;
using ResellerSystem.Modules.Inventory.Application;
using ResellerSystem.Modules.Inventory.Data;
using ResellerSystem.Server.Application.Audit;
using ResellerSystem.Server.Application.Exceptions;
using ResellerSystem.Server.Domain.Abstractions;
using Xunit;

namespace ResellerSystem.Modules.Inventory.Tests;

/// <summary>
/// Only covers the validation paths in PurchaseService.CreateAsync that run
/// BEFORE it touches the database, matching the existing convention in
/// InventoryServiceValidationTests.cs (a real DB-backed test would need
/// Postgres via Testcontainers, which this project doesn't have yet). The
/// allocation math itself (rounding, proportional/equal/manual modes, the
/// $10/3 exact-reconciliation guarantee) is covered separately and far more
/// thoroughly in MoneyAllocatorTests/PurchaseAllocationCalculatorTests,
/// since that's pure logic with no DB dependency at all — the higher-value
/// place to put this feature's test weight. The line-reconciliation safety
/// gates (quantity reduction/line removal blocked while an Item is
/// Listed/Sold) can only be exercised once a Purchase already exists in a
/// real database, so they're out of scope for this tier too.
/// </summary>
public class PurchaseServiceValidationTests
{
    private readonly IInventoryDbContextFactory _dbContextFactory = Substitute.For<IInventoryDbContextFactory>();
    private readonly IAuditLogger _auditLogger = Substitute.For<IAuditLogger>();
    private readonly ICurrentUserContext _currentUser = Substitute.For<ICurrentUserContext>();
    private readonly PurchaseService _sut;

    public PurchaseServiceValidationTests()
    {
        _sut = new PurchaseService(_dbContextFactory, _auditLogger, _currentUser);
    }

    [Fact]
    public async Task CreateAsync_rejects_blank_source_name_without_touching_database()
    {
        var request = MakeRequest(sourceName: "   ");

        var act = async () => await _sut.CreateAsync(request);

        await act.Should().ThrowAsync<ValidationFailedException>();
        _dbContextFactory.DidNotReceive().CreateForCurrentTenant();
    }

    [Fact]
    public async Task CreateAsync_rejects_blank_purchase_type_without_touching_database()
    {
        var request = MakeRequest(purchaseType: "");

        var act = async () => await _sut.CreateAsync(request);

        await act.Should().ThrowAsync<ValidationFailedException>();
        _dbContextFactory.DidNotReceive().CreateForCurrentTenant();
    }

    [Fact]
    public async Task CreateAsync_rejects_a_purchase_with_no_item_lines_without_touching_database()
    {
        var request = MakeRequest(itemLines: Array.Empty<PurchaseItemLineInput>());

        var act = async () => await _sut.CreateAsync(request);

        await act.Should().ThrowAsync<ValidationFailedException>();
        _dbContextFactory.DidNotReceive().CreateForCurrentTenant();
    }

    [Fact]
    public async Task CreateAsync_rejects_manual_allocation_that_does_not_reconcile_unless_AllowDifference_is_set()
    {
        var request = MakeRequest(
            itemLines: new[]
            {
                new PurchaseItemLineInput { ItemName = "A", Quantity = 1, UnitPurchaseCost = 100m, ManualAllocatedExpenses = 1m }
            },
            expenseLines: new[] { new PurchaseExpenseLineInput { ExpenseType = "Other", Amount = 20m } },
            expenseAllocationMethod: "Manual",
            allowDifference: false);

        var act = async () => await _sut.CreateAsync(request);

        await act.Should().ThrowAsync<ValidationFailedException>();
        _dbContextFactory.DidNotReceive().CreateForCurrentTenant();
    }

    [Fact]
    public void PreviewAllocation_never_touches_the_database()
    {
        var request = new PurchaseAllocationPreviewRequest
        {
            ItemLines = new[] { new PurchaseItemLineInput { ItemName = "A", Quantity = 2, UnitPurchaseCost = 5m } }
        };

        var result = _sut.PreviewAllocation(request);

        result.PhysicalItemsToCreate.Should().Be(2);
        _dbContextFactory.DidNotReceive().CreateForCurrentTenant();
    }

    private static CreatePurchaseFullRequest MakeRequest(
        string sourceName = "Estate Sale",
        string purchaseType = "TaxPaid",
        IReadOnlyList<PurchaseItemLineInput>? itemLines = null,
        IReadOnlyList<PurchaseExpenseLineInput>? expenseLines = null,
        string expenseAllocationMethod = "Proportional",
        bool allowDifference = false) => new()
    {
        PurchaseDate = DateOnly.FromDateTime(DateTime.Today),
        SourceName = sourceName,
        PurchaseType = purchaseType,
        ItemLines = itemLines ?? new[] { new PurchaseItemLineInput { ItemName = "Widget", Quantity = 1, UnitPurchaseCost = 10m } },
        ExpenseLines = expenseLines ?? Array.Empty<PurchaseExpenseLineInput>(),
        ExpenseAllocationMethod = expenseAllocationMethod,
        AllowDifference = allowDifference
    };
}
