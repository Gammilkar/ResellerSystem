using Microsoft.EntityFrameworkCore;
using ResellerSystem.Domain.Shared.Dto;
using ResellerSystem.Modules.Inventory.Data;
using ResellerSystem.Modules.Inventory.Domain;
using ResellerSystem.Server.Application.Exceptions;

namespace ResellerSystem.Modules.Inventory.Application;

public interface IInventoryService
{
    Task<PurchaseDto> CreatePurchaseAsync(CreatePurchaseRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<PurchaseDto>> ListPurchasesAsync(CancellationToken ct = default);
    Task<PurchaseDto> GetPurchaseAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<ItemDto>> ListItemsAsync(string? status, CancellationToken ct = default);
    Task<ItemDto> GetItemAsync(Guid id, CancellationToken ct = default);
    Task<ItemDto> UpdateItemAsync(Guid id, UpdateItemRequest request, CancellationToken ct = default);
    Task DeleteItemAsync(Guid id, CancellationToken ct = default);
}

public sealed class InventoryService : IInventoryService
{
    private readonly IInventoryDbContextFactory _dbContextFactory;

    public InventoryService(IInventoryDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<PurchaseDto> CreatePurchaseAsync(CreatePurchaseRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.SourceName))
            throw new ValidationFailedException(new[] { "Source name is required." });
        if (request.TotalAmount < 0)
            throw new ValidationFailedException(new[] { "Total amount cannot be negative." });
        if (request.Quantity < 1)
            throw new ValidationFailedException(new[] { "Quantity must be at least 1." });
        if (string.IsNullOrWhiteSpace(request.ItemName))
            throw new ValidationFailedException(new[] { "Item name is required." });

        await using var db = _dbContextFactory.CreateForCurrentTenant();

        var purchase = Purchase.CreateNew(
            request.PurchaseDate, request.SourceName, request.TotalAmount,
            request.SalesTaxAmount, request.SalesTaxRate, request.PaymentMethod,
            request.UsedResellerPermit, request.Comment);

        db.Purchases.Add(purchase);

        // Even split of cost across all requested units — Architecture Plan
        // v0.1 section 10: each physical unit is its own Item row.
        var costPerItem = Math.Round(request.TotalAmount / request.Quantity, 2, MidpointRounding.AwayFromZero);
        for (var i = 0; i < request.Quantity; i++)
        {
            var item = Item.CreateNew(purchase.Id, request.ItemName, request.CategoryName, costPerItem, null);
            db.Items.Add(item);
        }

        await db.SaveChangesAsync(ct);

        return await GetPurchaseAsync(purchase.Id, ct);
    }

    public async Task<IReadOnlyList<PurchaseDto>> ListPurchasesAsync(CancellationToken ct = default)
    {
        await using var db = _dbContextFactory.CreateForCurrentTenant();

        return await db.Purchases
            .OrderByDescending(p => p.PurchaseDate)
            .Select(p => ToDto(p, p.Items.Count))
            .ToListAsync(ct);
    }

    public async Task<PurchaseDto> GetPurchaseAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = _dbContextFactory.CreateForCurrentTenant();

        var purchase = await db.Purchases.Include(p => p.Items).FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("PURCHASE_NOT_FOUND", "Purchase was not found.");

        return ToDto(purchase, purchase.Items.Count);
    }

    public async Task<IReadOnlyList<ItemDto>> ListItemsAsync(string? status, CancellationToken ct = default)
    {
        await using var db = _dbContextFactory.CreateForCurrentTenant();

        var query = db.Items.AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(i => i.Status == status);
        }

        return await query
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => ToDto(i))
            .ToListAsync(ct);
    }

    public async Task<ItemDto> GetItemAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = _dbContextFactory.CreateForCurrentTenant();

        var item = await db.Items.FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw new NotFoundException("ITEM_NOT_FOUND", "Item was not found.");

        return ToDto(item);
    }

    public async Task<ItemDto> UpdateItemAsync(Guid id, UpdateItemRequest request, CancellationToken ct = default)
    {
        await using var db = _dbContextFactory.CreateForCurrentTenant();

        var item = await db.Items.FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw new NotFoundException("ITEM_NOT_FOUND", "Item was not found.");

        if (request.Name is not null) item.Name = request.Name.Trim();
        if (request.CategoryName is not null) item.CategoryName = request.CategoryName;
        if (request.Status is not null) item.Status = request.Status;
        if (request.CostBasisOverride is not null) item.CostBasisOverride = request.CostBasisOverride;
        if (request.Notes is not null) item.Notes = request.Notes;
        item.Touch();

        await db.SaveChangesAsync(ct);
        return ToDto(item);
    }

    public async Task DeleteItemAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = _dbContextFactory.CreateForCurrentTenant();

        var item = await db.Items.FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw new NotFoundException("ITEM_NOT_FOUND", "Item was not found.");

        item.SoftDelete(); // never a hard delete — Architecture Plan v0.1 section 47
        await db.SaveChangesAsync(ct);
    }

    private static PurchaseDto ToDto(Purchase p, int itemCount) => new()
    {
        Id = p.Id,
        PurchaseDate = p.PurchaseDate,
        SourceName = p.SourceName,
        TotalAmount = p.TotalAmount,
        SalesTaxAmount = p.SalesTaxAmount,
        SalesTaxRate = p.SalesTaxRate,
        PaymentMethod = p.PaymentMethod,
        UsedResellerPermit = p.UsedResellerPermit,
        Comment = p.Comment,
        ItemCount = itemCount,
        CreatedAt = p.CreatedAt
    };

    private static ItemDto ToDto(Item i) => new()
    {
        Id = i.Id,
        ItemNumber = i.ItemNumber,
        PurchaseId = i.PurchaseId,
        Name = i.Name,
        CategoryName = i.CategoryName,
        Status = i.Status,
        CostBasisCalculated = i.CostBasisCalculated,
        CostBasisOverride = i.CostBasisOverride,
        EffectiveCostBasis = i.EffectiveCostBasis,
        Notes = i.Notes,
        CreatedAt = i.CreatedAt
    };
}
