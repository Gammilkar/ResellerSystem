using FluentAssertions;
using NSubstitute;
using ResellerSystem.Domain.Shared.Dto;
using ResellerSystem.Modules.Sales.Application;
using ResellerSystem.Modules.Sales.Data;
using ResellerSystem.Modules.Sales.Domain;
using ResellerSystem.Server.Application.Databases;
using ResellerSystem.Server.Application.Exceptions;
using ResellerSystem.Server.Data.Configuration;
using Xunit;

namespace ResellerSystem.Modules.Sales.Tests;

public class SaleDomainTests
{
    [Fact]
    public void CreateNew_computes_gross_transaction_amount_from_components()
    {
        var sale = Sale.CreateNew(
            itemId: Guid.NewGuid(), listingId: null, marketplace: "eBay", marketplaceAccount: null,
            orderId: null, transactionId: null, saleDate: DateOnly.FromDateTime(DateTime.Today),
            itemSalePrice: 100m, buyerPaidShipping: 10m, buyerPaidSalesTax: 8m,
            handling: 2m, sellerDiscount: 5m, marketplaceCollectedTax: 8m,
            payoutAmount: 92m, quantity: 1, paymentMethod: null);

        // 100 + 10 + 8 + 2 - 5 = 115
        sale.GrossTransactionAmount.Should().Be(115m);
    }

    [Fact]
    public void CreateNew_keeps_gross_and_payout_as_independent_values()
    {
        var sale = Sale.CreateNew(
            Guid.NewGuid(), null, "eBay", null, null, null, DateOnly.FromDateTime(DateTime.Today),
            itemSalePrice: 100m, buyerPaidShipping: 0, buyerPaidSalesTax: 0,
            handling: 0, sellerDiscount: 0, marketplaceCollectedTax: 0,
            payoutAmount: 85m, quantity: 1, paymentMethod: null);

        sale.GrossTransactionAmount.Should().Be(100m);
        sale.PayoutAmount.Should().Be(85m);
        sale.GrossTransactionAmount.Should().NotBe(sale.PayoutAmount);
    }
}

public class SalesServiceValidationTests
{
    private readonly ISalesDbContextFactory _dbContextFactory = Substitute.For<ISalesDbContextFactory>();
    private readonly IItemCostBasisReader _costBasisReader = Substitute.For<IItemCostBasisReader>();
    private readonly ConnectionStringFactory _connectionStringFactory = null!; // not needed for validation-only paths
    private readonly ICurrentTenantAccessor _tenantAccessor = Substitute.For<ICurrentTenantAccessor>();

    private SalesService CreateSut() =>
        new(_dbContextFactory, _costBasisReader, _connectionStringFactory, _tenantAccessor);

    [Fact]
    public async Task CreateSaleAsync_rejects_negative_item_sale_price_without_touching_database()
    {
        var sut = CreateSut();
        var request = MakeSaleRequest(itemSalePrice: -1);

        var act = async () => await sut.CreateSaleAsync(request);

        await act.Should().ThrowAsync<ValidationFailedException>();
        _dbContextFactory.DidNotReceive().CreateForCurrentTenant();
    }

    [Fact]
    public async Task CreateSaleAsync_rejects_blank_marketplace()
    {
        var sut = CreateSut();
        var request = MakeSaleRequest(marketplace: "  ");

        var act = async () => await sut.CreateSaleAsync(request);

        await act.Should().ThrowAsync<ValidationFailedException>();
    }

    [Fact]
    public async Task CreateSaleAsync_rejects_quantity_below_one()
    {
        var sut = CreateSut();
        var request = MakeSaleRequest(quantity: 0);

        var act = async () => await sut.CreateSaleAsync(request);

        await act.Should().ThrowAsync<ValidationFailedException>();
    }

    [Fact]
    public async Task CreateReturnAsync_rejects_negative_refund_amount()
    {
        var sut = CreateSut();
        var request = new CreateReturnRequest
        {
            SaleId = Guid.NewGuid(),
            ItemId = Guid.NewGuid(),
            ReturnDate = DateOnly.FromDateTime(DateTime.Today),
            RefundToBuyer = -1,
            PhysicallyReturned = false
        };

        var act = async () => await sut.CreateReturnAsync(request);

        await act.Should().ThrowAsync<ValidationFailedException>();
        _dbContextFactory.DidNotReceive().CreateForCurrentTenant();
    }

    private static CreateSaleRequest MakeSaleRequest(
        string marketplace = "eBay", decimal itemSalePrice = 50m, int quantity = 1) => new()
    {
        ItemId = Guid.NewGuid(),
        Marketplace = marketplace,
        SaleDate = DateOnly.FromDateTime(DateTime.Today),
        ItemSalePrice = itemSalePrice,
        PayoutAmount = itemSalePrice,
        Quantity = quantity
    };
}
