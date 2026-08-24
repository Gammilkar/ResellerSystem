using Microsoft.EntityFrameworkCore;
using ResellerSystem.Domain.Shared.Dto;
using ResellerSystem.Modules.Sales.Data;
using ResellerSystem.Modules.Sales.Domain;
using ResellerSystem.Server.Application.Databases;
using ResellerSystem.Server.Application.Exceptions;
using ResellerSystem.Server.Data.Configuration;

namespace ResellerSystem.Modules.Sales.Application;

public interface ISalesService
{
    Task<ListingDto> CreateListingAsync(CreateListingRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<ListingDto>> ListListingsAsync(CancellationToken ct = default);

    Task<SaleDto> CreateSaleAsync(CreateSaleRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<SaleDto>> ListSalesAsync(CancellationToken ct = default);
    Task<SaleDto> GetSaleAsync(Guid id, CancellationToken ct = default);
    Task<SaleFeeDto> AddFeeAsync(Guid saleId, CreateSaleFeeRequest request, CancellationToken ct = default);
    Task<SaleFinancialsDto> GetFinancialsAsync(Guid saleId, CancellationToken ct = default);

    Task<ReturnDto> CreateReturnAsync(CreateReturnRequest request, CancellationToken ct = default);
}

public sealed class SalesService : ISalesService
{
    private readonly ISalesDbContextFactory _dbContextFactory;
    private readonly IItemCostBasisReader _costBasisReader;
    private readonly ConnectionStringFactory _connectionStringFactory;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public SalesService(
        ISalesDbContextFactory dbContextFactory,
        IItemCostBasisReader costBasisReader,
        ConnectionStringFactory connectionStringFactory,
        ICurrentTenantAccessor tenantAccessor)
    {
        _dbContextFactory = dbContextFactory;
        _costBasisReader = costBasisReader;
        _connectionStringFactory = connectionStringFactory;
        _tenantAccessor = tenantAccessor;
    }

    public async Task<ListingDto> CreateListingAsync(CreateListingRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Marketplace))
            throw new ValidationFailedException(new[] { "Marketplace is required." });

        await using var db = _dbContextFactory.CreateForCurrentTenant();
        var listing = Listing.CreateNew(request.ItemId, request.Marketplace, request.MarketplaceAccount, request.ListingPrice);
        db.Listings.Add(listing);
        await db.SaveChangesAsync(ct);

        return ToDto(listing);
    }

    public async Task<IReadOnlyList<ListingDto>> ListListingsAsync(CancellationToken ct = default)
    {
        await using var db = _dbContextFactory.CreateForCurrentTenant();
        return await db.Listings.OrderByDescending(l => l.CreatedAt).Select(l => ToDto(l)).ToListAsync(ct);
    }

    public async Task<SaleDto> CreateSaleAsync(CreateSaleRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Marketplace))
            throw new ValidationFailedException(new[] { "Marketplace is required." });
        if (request.ItemSalePrice < 0)
            throw new ValidationFailedException(new[] { "Item sale price cannot be negative." });
        if (request.Quantity < 1)
            throw new ValidationFailedException(new[] { "Quantity must be at least 1." });

        await using var db = _dbContextFactory.CreateForCurrentTenant();

        var sale = Sale.CreateNew(
            request.ItemId, request.ListingId, request.Marketplace, request.MarketplaceAccount,
            request.OrderId, request.TransactionId, request.SaleDate,
            request.ItemSalePrice, request.BuyerPaidShipping, request.BuyerPaidSalesTax,
            request.Handling, request.SellerDiscount, request.MarketplaceCollectedTax,
            request.PayoutAmount, request.Quantity, request.PaymentMethod);

        db.Sales.Add(sale);
        await db.SaveChangesAsync(ct);

        return await GetSaleAsync(sale.Id, ct);
    }

    public async Task<IReadOnlyList<SaleDto>> ListSalesAsync(CancellationToken ct = default)
    {
        await using var db = _dbContextFactory.CreateForCurrentTenant();
        var sales = await db.Sales.Include(s => s.Fees).OrderByDescending(s => s.SaleDate).ToListAsync(ct);
        return sales.Select(ToDto).ToList();
    }

    public async Task<SaleDto> GetSaleAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = _dbContextFactory.CreateForCurrentTenant();
        var sale = await db.Sales.Include(s => s.Fees).FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NotFoundException("SALE_NOT_FOUND", "Sale was not found.");
        return ToDto(sale);
    }

    public async Task<SaleFeeDto> AddFeeAsync(Guid saleId, CreateSaleFeeRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.FeeType))
            throw new ValidationFailedException(new[] { "Fee type is required." });

        await using var db = _dbContextFactory.CreateForCurrentTenant();

        var saleExists = await db.Sales.AnyAsync(s => s.Id == saleId, ct);
        if (!saleExists) throw new NotFoundException("SALE_NOT_FOUND", "Sale was not found.");

        var fee = SaleFee.CreateNew(saleId, request.FeeType, request.Amount, request.Rate, "manual");
        db.SaleFees.Add(fee);
        await db.SaveChangesAsync(ct);

        return new SaleFeeDto { Id = fee.Id, FeeType = fee.FeeType, Amount = fee.Amount, Rate = fee.Rate };
    }

    public async Task<SaleFinancialsDto> GetFinancialsAsync(Guid saleId, CancellationToken ct = default)
    {
        await using var db = _dbContextFactory.CreateForCurrentTenant();
        var sale = await db.Sales.Include(s => s.Fees).FirstOrDefaultAsync(s => s.Id == saleId, ct)
            ?? throw new NotFoundException("SALE_NOT_FOUND", "Sale was not found.");

        // Seller revenue excludes buyer-paid sales tax collected on behalf
        // of the marketplace — that money was never the seller's (Architecture
        // Plan v0.1 section 35: "не считать эту сумму доходом продавца").
        var sellerGrossRevenue = sale.ItemSalePrice + sale.BuyerPaidShipping + sale.Handling - sale.SellerDiscount;
        var totalFees = sale.Fees.Sum(f => f.Amount);
        var netProceeds = sellerGrossRevenue - totalFees;

        var tenant = _tenantAccessor.Require();
        var connectionString = _connectionStringFactory.BuildTenantConnectionString(tenant.PhysicalDatabaseName);
        var costBasis = await _costBasisReader.GetEffectiveCostBasisAsync(connectionString, sale.ItemId, ct);

        decimal? netProfit = costBasis is not null ? netProceeds - costBasis.Value : null;
        decimal? roi = costBasis is > 0 ? Math.Round((netProfit!.Value / costBasis.Value) * 100, 2) : null;

        return new SaleFinancialsDto
        {
            SaleId = sale.Id,
            SellerGrossRevenue = sellerGrossRevenue,
            TotalMarketplaceFees = totalFees,
            NetProceeds = netProceeds,
            ItemCostBasis = costBasis,
            NetProfit = netProfit,
            RoiPercent = roi
        };
    }

    public async Task<ReturnDto> CreateReturnAsync(CreateReturnRequest request, CancellationToken ct = default)
    {
        if (request.RefundToBuyer < 0)
            throw new ValidationFailedException(new[] { "Refund amount cannot be negative." });

        await using var db = _dbContextFactory.CreateForCurrentTenant();

        var saleExists = await db.Sales.AnyAsync(s => s.Id == request.SaleId, ct);
        if (!saleExists) throw new NotFoundException("SALE_NOT_FOUND", "Sale was not found.");

        var ret = Return.CreateNew(
            request.SaleId, request.ItemId, request.ReturnDate, request.ReturnType,
            request.RefundToBuyer, request.RefundedShipping, request.MarketplaceFeeCredit,
            request.ReturnShippingCost, request.OtherExpense, request.PhysicallyReturned,
            request.ConditionOnReturn, request.Comment);

        db.Returns.Add(ret);
        await db.SaveChangesAsync(ct);

        return new ReturnDto
        {
            Id = ret.Id,
            SaleId = ret.SaleId,
            ReturnDate = ret.ReturnDate,
            RefundToBuyer = ret.RefundToBuyer,
            PhysicallyReturned = ret.PhysicallyReturned
        };
    }

    private static ListingDto ToDto(Listing l) => new()
    {
        Id = l.Id,
        ItemId = l.ItemId,
        Marketplace = l.Marketplace,
        MarketplaceAccount = l.MarketplaceAccount,
        ListingPrice = l.ListingPrice,
        Status = l.Status,
        CreatedAt = l.CreatedAt
    };

    private static SaleDto ToDto(Sale s) => new()
    {
        Id = s.Id,
        ItemId = s.ItemId,
        ListingId = s.ListingId,
        Marketplace = s.Marketplace,
        OrderId = s.OrderId,
        SaleDate = s.SaleDate,
        ItemSalePrice = s.ItemSalePrice,
        BuyerPaidShipping = s.BuyerPaidShipping,
        BuyerPaidSalesTax = s.BuyerPaidSalesTax,
        Handling = s.Handling,
        SellerDiscount = s.SellerDiscount,
        GrossTransactionAmount = s.GrossTransactionAmount,
        MarketplaceCollectedTax = s.MarketplaceCollectedTax,
        PayoutAmount = s.PayoutAmount,
        Quantity = s.Quantity,
        Fees = s.Fees.Select(f => new SaleFeeDto { Id = f.Id, FeeType = f.FeeType, Amount = f.Amount, Rate = f.Rate }).ToList()
    };
}
