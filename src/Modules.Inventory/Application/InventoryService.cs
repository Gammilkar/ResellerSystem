using Microsoft.EntityFrameworkCore;
using ResellerSystem.Domain.Shared.Dto;
using ResellerSystem.Modules.Inventory.Data;
using ResellerSystem.Modules.Inventory.Domain;
using ResellerSystem.Server.Application.Audit;
using ResellerSystem.Server.Application.Exceptions;
using ResellerSystem.Server.Domain.Abstractions;

namespace ResellerSystem.Modules.Inventory.Application;

public interface IInventoryService
{
    Task<PurchaseDto> CreatePurchaseAsync(CreatePurchaseRequest request, CancellationToken ct = default);
    Task<PurchaseDto> CreatePurchaseHeaderAsync(CreatePurchaseHeaderRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<PurchaseDto>> ListPurchasesAsync(CancellationToken ct = default);
    Task<PurchaseDto> GetPurchaseAsync(Guid id, CancellationToken ct = default);
    Task<PurchaseDto> UpdatePurchaseAsync(Guid id, UpdatePurchaseRequest request, CancellationToken ct = default);

    Task<ItemDto> AddItemToPurchaseAsync(Guid purchaseId, AddItemToPurchaseRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<ItemDto>> ListItemsAsync(string? status, CancellationToken ct = default);
    Task<ItemDto> GetItemAsync(Guid id, CancellationToken ct = default);
    Task<ItemDto> UpdateItemAsync(Guid id, UpdateItemRequest request, CancellationToken ct = default);
    Task DeleteItemAsync(Guid id, CancellationToken ct = default);
}

public sealed class InventoryService : IInventoryService
{
    private readonly IInventoryDbContextFactory _dbContextFactory;
    private readonly IAuditLogger _auditLogger;
    private readonly ICurrentUserContext _currentUser;

    public InventoryService(IInventoryDbContextFactory dbContextFactory, IAuditLogger auditLogger, ICurrentUserContext currentUser)
    {
        _dbContextFactory = dbContextFactory;
        _auditLogger = auditLogger;
        _currentUser = currentUser;
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
            request.PurchaseType, request.Comment);

        db.Purchases.Add(purchase);

        // Even split of cost across all requested units — Architecture Plan
        // v0.1 section 10: each physical unit is its own Item row.
        var costPerItem = Math.Round(request.TotalAmount / request.Quantity, 2, MidpointRounding.AwayFromZero);
        var items = new List<Item>();
        for (var i = 0; i < request.Quantity; i++)
        {
            var item = Item.CreateNew(purchase.Id, request.ItemName, request.CategoryName, costPerItem, null);
            db.Items.Add(item);
            items.Add(item);
        }

        await db.SaveChangesAsync(ct);

        var username = _currentUser.DisplayName;
        var auditEntries = new List<AuditEntry> { new("Purchase", purchase.Id, "Created", username, "manual") };
        auditEntries.AddRange(items.Select(i => new AuditEntry("Item", i.Id, "Created", username, "manual")));
        await _auditLogger.LogManyAsync(auditEntries, ct);

        return await GetPurchaseAsync(purchase.Id, ct);
    }

    public async Task<PurchaseDto> CreatePurchaseHeaderAsync(CreatePurchaseHeaderRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.SourceName))
            throw new ValidationFailedException(new[] { "Source name is required." });
        if (request.TotalAmount < 0)
            throw new ValidationFailedException(new[] { "Total amount cannot be negative." });

        await using var db = _dbContextFactory.CreateForCurrentTenant();

        var purchase = Purchase.CreateNew(
            request.PurchaseDate, request.SourceName, request.TotalAmount,
            request.SalesTaxAmount, request.SalesTaxRate, request.PaymentMethod,
            request.PurchaseType, request.Comment);

        db.Purchases.Add(purchase);
        await db.SaveChangesAsync(ct);

        await _auditLogger.LogAsync(new AuditEntry("Purchase", purchase.Id, "Created", _currentUser.DisplayName, "manual"), ct);
        return ToDto(purchase, 0);
    }

    public async Task<ItemDto> AddItemToPurchaseAsync(Guid purchaseId, AddItemToPurchaseRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationFailedException(new[] { "Item name is required." });

        await using var db = _dbContextFactory.CreateForCurrentTenant();

        var purchaseExists = await db.Purchases.AnyAsync(p => p.Id == purchaseId, ct);
        if (!purchaseExists) throw new NotFoundException("PURCHASE_NOT_FOUND", "Purchase was not found.");

        var item = Item.CreateNew(purchaseId, request.Name, request.CategoryName, request.CostBasis, request.Notes);
        db.Items.Add(item);
        await db.SaveChangesAsync(ct);

        await _auditLogger.LogAsync(new AuditEntry("Item", item.Id, "Created", _currentUser.DisplayName, "manual"), ct);
        return ToDto(item);
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

    public async Task<PurchaseDto> UpdatePurchaseAsync(Guid id, UpdatePurchaseRequest request, CancellationToken ct = default)
    {
        await using var db = _dbContextFactory.CreateForCurrentTenant();

        var purchase = await db.Purchases.Include(p => p.Items).FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("PURCHASE_NOT_FOUND", "Purchase was not found.");

        var username = _currentUser.DisplayName;
        var changes = new List<AuditEntry>();
        void TrackChange(string field, string? oldValue, string? newValue)
        {
            if (oldValue != newValue) changes.Add(new AuditEntry("Purchase", purchase.Id, "Updated", username, "manual", field, oldValue, newValue));
        }

        if (request.SupplierId is not null)
        {
            var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == request.SupplierId, ct)
                ?? throw new NotFoundException("SUPPLIER_NOT_FOUND", "Supplier was not found.");

            TrackChange("SupplierId", purchase.SupplierId?.ToString(), supplier.Id.ToString());
            purchase.SupplierId = supplier.Id;

            // source_name is a denormalized snapshot of the supplier's name
            // at assignment time, not a live join — see 0003_suppliers.sql.
            TrackChange("SourceName", purchase.SourceName, supplier.Name);
            purchase.SourceName = supplier.Name;
        }
        else if (request.SourceName is not null)
        {
            TrackChange("SourceName", purchase.SourceName, request.SourceName.Trim());
            purchase.SourceName = request.SourceName.Trim();
        }

        if (request.PurchaseType is not null) { TrackChange("PurchaseType", purchase.PurchaseType, request.PurchaseType); purchase.PurchaseType = request.PurchaseType; }
        if (request.PurchaseDate is not null)
        {
            TrackChange("PurchaseDate", purchase.PurchaseDate.ToString("O"), request.PurchaseDate.Value.ToString("O"));
            purchase.PurchaseDate = request.PurchaseDate.Value;
        }
        purchase.UpdatedBy = username;
        purchase.Touch();

        await db.SaveChangesAsync(ct);
        if (changes.Count > 0) await _auditLogger.LogManyAsync(changes, ct);

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

        var username = _currentUser.DisplayName;
        var changes = new List<AuditEntry>();
        void TrackChange(string field, string? oldValue, string? newValue)
        {
            if (oldValue != newValue) changes.Add(new AuditEntry("Item", item.Id, "Updated", username, "manual", field, oldValue, newValue));
        }

        if (request.Name is not null) { TrackChange("Name", item.Name, request.Name.Trim()); item.Name = request.Name.Trim(); }
        if (request.CategoryName is not null) { TrackChange("CategoryName", item.CategoryName, request.CategoryName); item.CategoryName = request.CategoryName; }
        if (request.Status is not null) { TrackChange("Status", item.Status, request.Status); item.Status = request.Status; }
        if (request.CostBasisOverride is not null)
        {
            TrackChange("CostBasisOverride", item.CostBasisOverride?.ToString(), request.CostBasisOverride?.ToString());
            item.CostBasisOverride = request.CostBasisOverride;
        }
        if (request.Notes is not null) { TrackChange("Notes", item.Notes, request.Notes); item.Notes = request.Notes; }
        item.Touch();

        await db.SaveChangesAsync(ct);
        if (changes.Count > 0) await _auditLogger.LogManyAsync(changes, ct);

        return ToDto(item);
    }

    public async Task DeleteItemAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = _dbContextFactory.CreateForCurrentTenant();

        var item = await db.Items.FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw new NotFoundException("ITEM_NOT_FOUND", "Item was not found.");

        item.SoftDelete(); // never a hard delete — Architecture Plan v0.1 section 47
        await db.SaveChangesAsync(ct);
        await _auditLogger.LogAsync(new AuditEntry("Item", item.Id, "Deleted", _currentUser.DisplayName, "manual"), ct);
    }

    private static PurchaseDto ToDto(Purchase p, int itemCount) => new()
    {
        Id = p.Id,
        PurchaseDate = p.PurchaseDate,
        SourceName = p.SourceName,
        SupplierId = p.SupplierId,
        TotalAmount = p.TotalAmount,
        SalesTaxAmount = p.SalesTaxAmount,
        SalesTaxRate = p.SalesTaxRate,
        PaymentMethod = p.PaymentMethod,
        UsedResellerPermit = p.UsedResellerPermit,
        PurchaseType = p.PurchaseType,
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
