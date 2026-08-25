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
/// Only covers the validation paths in InventoryService.CreatePurchaseAsync
/// that run BEFORE it touches the database (constructs an
/// IInventoryDbContextFactory but never calls it for invalid input,
/// verified below) — a real database-backed test would need Postgres via
/// Testcontainers, matching the pattern in Server.Data.Tests, which this
/// project does not yet have (see KNOWN_LIMITATIONS.md).
/// </summary>
public class InventoryServiceValidationTests
{
    private readonly IInventoryDbContextFactory _dbContextFactory = Substitute.For<IInventoryDbContextFactory>();
    private readonly IAuditLogger _auditLogger = Substitute.For<IAuditLogger>();
    private readonly ICurrentUserContext _currentUser = Substitute.For<ICurrentUserContext>();
    private readonly InventoryService _sut;

    public InventoryServiceValidationTests()
    {
        _sut = new InventoryService(_dbContextFactory, _auditLogger, _currentUser);
    }

    [Fact]
    public async Task CreatePurchaseAsync_rejects_blank_source_name_without_touching_database()
    {
        var request = MakeRequest(sourceName: "   ");

        var act = async () => await _sut.CreatePurchaseAsync(request);

        await act.Should().ThrowAsync<ValidationFailedException>();
        _dbContextFactory.DidNotReceive().CreateForCurrentTenant();
    }

    [Fact]
    public async Task CreatePurchaseAsync_rejects_negative_total_amount()
    {
        var request = MakeRequest(totalAmount: -1);

        var act = async () => await _sut.CreatePurchaseAsync(request);

        await act.Should().ThrowAsync<ValidationFailedException>();
        _dbContextFactory.DidNotReceive().CreateForCurrentTenant();
    }

    [Fact]
    public async Task CreatePurchaseAsync_rejects_quantity_below_one()
    {
        var request = MakeRequest(quantity: 0);

        var act = async () => await _sut.CreatePurchaseAsync(request);

        await act.Should().ThrowAsync<ValidationFailedException>();
        _dbContextFactory.DidNotReceive().CreateForCurrentTenant();
    }

    [Fact]
    public async Task CreatePurchaseAsync_rejects_blank_item_name()
    {
        var request = MakeRequest(itemName: "");

        var act = async () => await _sut.CreatePurchaseAsync(request);

        await act.Should().ThrowAsync<ValidationFailedException>();
        _dbContextFactory.DidNotReceive().CreateForCurrentTenant();
    }

    private static CreatePurchaseRequest MakeRequest(
        string sourceName = "Goodwill", decimal totalAmount = 100m, string itemName = "Widget", int quantity = 1) => new()
    {
        PurchaseDate = DateOnly.FromDateTime(DateTime.Today),
        SourceName = sourceName,
        TotalAmount = totalAmount,
        ItemName = itemName,
        Quantity = quantity
    };
}
