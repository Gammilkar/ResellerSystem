using Microsoft.EntityFrameworkCore;
using ResellerSystem.Domain.Shared.Dto;
using ResellerSystem.Modules.Sales.Data;
using ResellerSystem.Modules.Sales.Domain;
using ResellerSystem.Server.Application.Audit;
using ResellerSystem.Server.Application.Databases;
using ResellerSystem.Server.Application.Exceptions;
using ResellerSystem.Server.Data.Configuration;
using ResellerSystem.Server.Domain.Abstractions;

namespace ResellerSystem.Modules.Sales.Application;

public interface ISalesService
{
    Task<ListingDto> CreateListingAsync(CreateListingRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<ListingDto>> ListListingsAsync(CancellationToken ct = default);
    Task<ListingDto> UpdateListingAsync(Guid id, UpdateListingRequest request, CancellationToken ct = default);

    Task<SaleDto> CreateSaleAsync(CreateSaleRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<SaleDto>> ListSalesAsync(CancellationToken ct = default);
    Task<SaleDto> GetSaleAsync(Guid id, CancellationToken ct = default);
    Task<SaleDto> UpdateSaleAsync(Guid id, UpdateSaleRequest request, CancellationToken ct = default);
    Task<SaleFeeDto> AddFeeAsync(Guid saleId, CreateSaleFeeRequest request, CancellationToken ct = default);
    Task<SaleFinancialsDto> GetFinancialsAsync(Guid saleId, CancellationToken ct = default);

    Task<ReturnDto> CreateReturnAsync(CreateReturnRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<ReturnDto>> ListReturnsAsync(CancellationToken ct = default);
}

public sealed class SalesService : ISalesService
{
    private readonly ISalesDbContextFactory _dbContextFactory;
    private readonly IItemCostBasisReader _costBasisReader;
    private readonly ConnectionStringFactory _connectionStringFactory;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly IAuditLogger _auditLogger;
    private readonly ICurrentUserContext _currentUser;

    public SalesService(
        ISalesDbContextFactory dbContextFactory,
        IItemCostBasisReader costBasisReader,
        ConnectionStringFactory connectionStringFactory,
        ICurrentTenantAccessor tenantAccessor,
        IAuditLogger auditLogger,
        ICurrentUserContext currentUser)
    {
        _dbContextFactory = dbContextFactory;
        _costBasisReader = costBasisReader;
        _connectionStringFactory = connectionStringFactory;
        _tenantAccessor = tenantAccessor;
        _auditLogger = auditLogger;
        _currentUser = currentUser;
    }

    public async Task<ListingDto> CreateListingAsync(CreateListingRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Marketplace))
            throw new ValidationFailedException(new[] { "Marketplace is required." });

        await using var db = _dbContextFactory.CreateForCurrentTenant();
        var listing = Listing.CreateNew(request.ItemId, request.Marketplace, request.MarketplaceAccount, request.ListingPrice);
        listing.ExternalListingId = request.ExternalListingId;
        if (request.PublishedDate is not null) listing.PublishedDate = request.PublishedDate;
        listing.Promoted = request.Promoted;
        listing.PromotedRate = request.PromotedRate;
        listing.Url = request.Url;
        listing.EndDate = request.EndDate;
        listing.CreatedBy = _currentUser.DisplayName;
        listing.UpdatedBy = _currentUser.DisplayName;
        db.Listings.Add(listing);
        await db.SaveChangesAsync(ct);

        await _auditLogger.LogAsync(new AuditEntry("Listing", listing.Id, "Created", _currentUser.DisplayName, "manual"), ct);
        return ToDto(listing);
    }

    public async Task<IReadOnlyList<ListingDto>> ListListingsAsync(CancellationToken ct = default)
    {
        await using var db = _dbContextFactory.CreateForCurrentTenant();
        return await db.Listings.OrderByDescending(l => l.CreatedAt).Select(l => ToDto(l)).ToListAsync(ct);
    }

    public async Task<ListingDto> UpdateListingAsync(Guid id, UpdateListingRequest request, CancellationToken ct = default)
    {
        await using var db = _dbContextFactory.CreateForCurrentTenant();

        var listing = await db.Listings.FirstOrDefaultAsync(l => l.Id == id, ct)
            ?? throw new NotFoundException("LISTING_NOT_FOUND", "Listing was not found.");

        var username = _currentUser.DisplayName;
        var changes = new List<AuditEntry>();
        void TrackChange(string field, string? oldValue, string? newValue)
        {
            if (oldValue != newValue) changes.Add(new AuditEntry("Listing", listing.Id, "Updated", username, "manual", field, oldValue, newValue));
        }

        if (request.Marketplace is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Marketplace))
                throw new ValidationFailedException(new[] { "Marketplace is required." });
            TrackChange("Marketplace", listing.Marketplace, request.Marketplace.Trim());
            listing.Marketplace = request.Marketplace.Trim();
        }
        if (request.PublishedDate is not null)
        {
            TrackChange("PublishedDate", listing.PublishedDate?.ToString("O"), request.PublishedDate.Value.ToString("O"));
            listing.PublishedDate = request.PublishedDate;
        }
        listing.UpdatedBy = username;
        listing.Touch();

        await db.SaveChangesAsync(ct);
        if (changes.Count > 0) await _auditLogger.LogManyAsync(changes, ct);

        return ToDto(listing);
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
        sale.DestinationState = request.DestinationState;
        sale.DestinationZip = request.DestinationZip;
        sale.CreatedBy = _currentUser.DisplayName;
        sale.UpdatedBy = _currentUser.DisplayName;

        db.Sales.Add(sale);
        await db.SaveChangesAsync(ct);

        await _auditLogger.LogAsync(new AuditEntry("Sale", sale.Id, "Created", _currentUser.DisplayName, "manual"), ct);
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

    public async Task<SaleDto> UpdateSaleAsync(Guid id, UpdateSaleRequest request, CancellationToken ct = default)
    {
        if (request.ItemSalePrice is < 0)
            throw new ValidationFailedException(new[] { "Item sale price cannot be negative." });

        await using var db = _dbContextFactory.CreateForCurrentTenant();

        var sale = await db.Sales.FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NotFoundException("SALE_NOT_FOUND", "Sale was not found.");

        var username = _currentUser.DisplayName;
        var changes = new List<AuditEntry>();
        void TrackChange(string field, string? oldValue, string? newValue)
        {
            if (oldValue != newValue) changes.Add(new AuditEntry("Sale", sale.Id, "Updated", username, "manual", field, oldValue, newValue));
        }

        if (request.SaleDate is not null)
        {
            TrackChange("SaleDate", sale.SaleDate.ToString("O"), request.SaleDate.Value.ToString("O"));
            sale.SaleDate = request.SaleDate.Value;
        }
        if (request.Marketplace is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Marketplace))
                throw new ValidationFailedException(new[] { "Marketplace is required." });
            TrackChange("Marketplace", sale.Marketplace, request.Marketplace.Trim());
            sale.Marketplace = request.Marketplace.Trim();
        }
        if (request.ItemSalePrice is not null)
        {
            TrackChange("ItemSalePrice", sale.ItemSalePrice.ToString(), request.ItemSalePrice.Value.ToString());
            sale.ItemSalePrice = request.ItemSalePrice.Value;

            // GrossTransactionAmount is stored, not computed on read (see
            // Sale.CreateNew) — recompute with the same formula so it never
            // silently drifts from a changed ItemSalePrice.
            var newGross = sale.ItemSalePrice + sale.BuyerPaidShipping + sale.BuyerPaidSalesTax + sale.Handling - sale.SellerDiscount;
            TrackChange("GrossTransactionAmount", sale.GrossTransactionAmount.ToString(), newGross.ToString());
            sale.GrossTransactionAmount = newGross;
        }
        sale.UpdatedBy = username;
        sale.Touch();

        await db.SaveChangesAsync(ct);
        if (changes.Count > 0) await _auditLogger.LogManyAsync(changes, ct);

        return await GetSaleAsync(sale.Id, ct);
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

        await _auditLogger.LogAsync(new AuditEntry("SaleFee", fee.Id, "Created", _currentUser.DisplayName, "manual"), ct);
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
        ret.CreatedBy = _currentUser.DisplayName;
        ret.UpdatedBy = _currentUser.DisplayName;

        db.Returns.Add(ret);
        await db.SaveChangesAsync(ct);

        await _auditLogger.LogAsync(new AuditEntry("Return", ret.Id, "Created", _currentUser.DisplayName, "manual"), ct);
        return ToDto(ret);
    }

    public async Task<IReadOnlyList<ReturnDto>> ListReturnsAsync(CancellationToken ct = default)
    {
        await using var db = _dbContextFactory.CreateForCurrentTenant();
        return await db.Returns.OrderByDescending(r => r.CreatedAt).Select(r => ToDto(r)).ToListAsync(ct);
    }

    private static ListingDto ToDto(Listing l) => new()
    {
        Id = l.Id,
        ItemId = l.ItemId,
        Marketplace = l.Marketplace,
        MarketplaceAccount = l.MarketplaceAccount,
        ExternalListingId = l.ExternalListingId,
        PublishedDate = l.PublishedDate,
        ListingPrice = l.ListingPrice,
        Promoted = l.Promoted,
        PromotedRate = l.PromotedRate,
        Status = l.Status,
        Url = l.Url,
        EndDate = l.EndDate,
        CreatedAt = l.CreatedAt
    };

    private static SaleDto ToDto(Sale s) => new()
    {
        Id = s.Id,
        ItemId = s.ItemId,
        ListingId = s.ListingId,
        Marketplace = s.Marketplace,
        OrderId = s.OrderId,
        TransactionId = s.TransactionId,
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
        DestinationState = s.DestinationState,
        DestinationZip = s.DestinationZip,
        Fees = s.Fees.Select(f => new SaleFeeDto { Id = f.Id, FeeType = f.FeeType, Amount = f.Amount, Rate = f.Rate }).ToList()
    };

    private static ReturnDto ToDto(Return r) => new()
    {
        Id = r.Id,
        SaleId = r.SaleId,
        ItemId = r.ItemId,
        ReturnDate = r.ReturnDate,
        ReturnType = r.ReturnType,
        RefundToBuyer = r.RefundToBuyer,
        RefundedShipping = r.RefundedShipping,
        MarketplaceFeeCredit = r.MarketplaceFeeCredit,
        ReturnShippingCost = r.ReturnShippingCost,
        OtherExpense = r.OtherExpense,
        PhysicallyReturned = r.PhysicallyReturned,
        ConditionOnReturn = r.ConditionOnReturn,
        Comment = r.Comment
    };
}
