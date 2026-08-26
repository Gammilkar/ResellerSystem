using Microsoft.EntityFrameworkCore;
using ResellerSystem.Domain.Shared.Dto;
using ResellerSystem.Modules.Inventory.Data;
using ResellerSystem.Modules.Inventory.Domain;
using ResellerSystem.Server.Application.Audit;
using ResellerSystem.Server.Application.Exceptions;
using ResellerSystem.Server.Domain.Abstractions;

namespace ResellerSystem.Modules.Inventory.Application;

/// <summary>The full purchase-intake workflow (Product Specification
/// §1-24) — separate from IInventoryService's older quick-entry
/// CreatePurchaseAsync (kept untouched for backward compatibility with the
/// grid's simple form and Import). One line (PurchaseItemLine) can carry a
/// Quantity > 1 and explodes into that many individual Item rows on save,
/// each with its own exact cost basis (MoneyAllocator/
/// PurchaseAllocationCalculator).</summary>
public interface IPurchaseService
{
    Task<PurchaseDetailDto> CreateAsync(CreatePurchaseFullRequest request, CancellationToken ct = default);
    Task<PurchaseDetailDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<PurchaseListRowDto>> ListAsync(PurchaseListFilterRequest? filter, CancellationToken ct = default);
    Task<PurchaseDetailDto> UpdateAsync(Guid id, UpdatePurchaseFullRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    PurchaseAllocationResult PreviewAllocation(PurchaseAllocationPreviewRequest request);
}

public sealed class PurchaseService : IPurchaseService
{
    /// <summary>Statuses an Item can still be in for its line/quantity to
    /// be safely reduced or removed — anything past this point (Listed,
    /// Sold, ...) may have a live marketplace listing or sale history this
    /// module has no cross-module way to clean up (Product Specification
    /// §23-24: "ничего не менять скрыто").</summary>
    private static readonly string[] SafelyRemovableStatuses =
        { ItemStatuses.Purchased, ItemStatuses.InStock, ItemStatuses.NotListed };

    private readonly IInventoryDbContextFactory _dbContextFactory;
    private readonly IAuditLogger _auditLogger;
    private readonly ICurrentUserContext _currentUser;

    public PurchaseService(IInventoryDbContextFactory dbContextFactory, IAuditLogger auditLogger, ICurrentUserContext currentUser)
    {
        _dbContextFactory = dbContextFactory;
        _auditLogger = auditLogger;
        _currentUser = currentUser;
    }

    public async Task<PurchaseDetailDto> CreateAsync(CreatePurchaseFullRequest request, CancellationToken ct = default)
    {
        ValidateHeader(request);
        var allocation = Allocate(request);
        if (!allocation.IsReadyToSave && !request.AllowDifference)
            throw new ValidationFailedException(allocation.ValidationErrors);

        await using var db = _dbContextFactory.CreateForCurrentTenant();
        var username = _currentUser.DisplayName;

        var sourceName = await ResolveSourceNameAsync(db, request.SourceName, request.SupplierId, ct);

        var purchase = Purchase.CreateNew(
            request.PurchaseDate, sourceName, allocation.TotalPurchaseCost,
            allocation.SalesTaxAmount, request.SalesTaxRate, request.PaymentMethod,
            request.PurchaseType, request.Comment);
        ApplyFullWorkflowFields(purchase, request, allocation);
        purchase.CreatedBy = username;
        purchase.UpdatedBy = username;
        db.Purchases.Add(purchase);

        var auditEntries = new List<AuditEntry> { new("Purchase", purchase.Id, "Created", username, "manual") };

        for (var i = 0; i < request.ItemLines.Count; i++)
        {
            var input = request.ItemLines[i];
            var calc = allocation.Lines[i];

            var line = PurchaseItemLine.CreateNew(purchase.Id, calc.LineNumber, input.ItemName, input.CategoryName,
                input.Quantity, input.UnitPurchaseCost, input.Notes);
            ApplyLineAllocation(line, calc, input);
            line.CreatedBy = username;
            line.UpdatedBy = username;
            db.PurchaseItemLines.Add(line);
            auditEntries.Add(new AuditEntry("PurchaseItemLine", line.Id, "Created", username, "manual"));

            foreach (var unitCostBasis in calc.UnitCostBases)
            {
                var item = Item.CreateNew(purchase.Id, input.ItemName, input.CategoryName, unitCostBasis, input.Notes);
                item.PurchaseItemLineId = line.Id;
                db.Items.Add(item);
                auditEntries.Add(new AuditEntry("Item", item.Id, "Created", username, "manual"));
            }
        }

        foreach (var expenseInput in request.ExpenseLines)
        {
            var expenseLine = PurchaseExpenseLine.CreateNew(purchase.Id, expenseInput.ExpenseType, expenseInput.Amount, expenseInput.Notes);
            expenseLine.CreatedBy = username;
            expenseLine.UpdatedBy = username;
            db.PurchaseExpenseLines.Add(expenseLine);
            auditEntries.Add(new AuditEntry("PurchaseExpenseLine", expenseLine.Id, "Created", username, "manual"));
        }

        await db.SaveChangesAsync(ct);
        await _auditLogger.LogManyAsync(auditEntries, ct);

        return await GetAsync(purchase.Id, ct);
    }

    public async Task<PurchaseDetailDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = _dbContextFactory.CreateForCurrentTenant();

        var purchase = await db.Purchases
            .Include(p => p.ItemLines)
            .Include(p => p.ExpenseLines)
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("PURCHASE_NOT_FOUND", "Purchase was not found.");

        return ToDetailDto(purchase);
    }

    public async Task<IReadOnlyList<PurchaseListRowDto>> ListAsync(PurchaseListFilterRequest? filter, CancellationToken ct = default)
    {
        await using var db = _dbContextFactory.CreateForCurrentTenant();

        var query = db.Purchases.Include(p => p.ExpenseLines).Include(p => p.Items).AsQueryable();

        if (filter is not null)
        {
            if (filter.DateFrom is { } from) query = query.Where(p => p.PurchaseDate >= from);
            if (filter.DateTo is { } to) query = query.Where(p => p.PurchaseDate <= to);
            if (!string.IsNullOrWhiteSpace(filter.SourceName)) query = query.Where(p => p.SourceName.Contains(filter.SourceName));
            if (!string.IsNullOrWhiteSpace(filter.PurchaseType)) query = query.Where(p => p.PurchaseType == filter.PurchaseType);
            if (filter.UsedResellerPermit is { } permit) query = query.Where(p => p.UsedResellerPermit == permit);
            if (!string.IsNullOrWhiteSpace(filter.PaymentMethod)) query = query.Where(p => p.PaymentMethod == filter.PaymentMethod);
            if (filter.MinTotalAmount is { } min) query = query.Where(p => p.TotalAmount >= min);
            if (filter.MaxTotalAmount is { } max) query = query.Where(p => p.TotalAmount <= max);
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var s = filter.Search;
                query = query.Where(p => p.SourceName.Contains(s) || (p.Comment != null && p.Comment.Contains(s)));
            }
        }

        var purchases = await query.OrderByDescending(p => p.PurchaseDate).ToListAsync(ct);

        return purchases.Select(p => new PurchaseListRowDto
        {
            Id = p.Id,
            PurchaseDate = p.PurchaseDate,
            SourceName = p.SourceName,
            PurchaseType = p.PurchaseType,
            UsedResellerPermit = p.UsedResellerPermit,
            PaymentMethod = p.PaymentMethod,
            MerchandiseSubtotal = p.MerchandiseSubtotal,
            SalesTaxAmount = p.SalesTaxAmount,
            TotalExpenses = p.ExpenseLines.Sum(e => e.Amount),
            TotalAmount = p.TotalAmount,
            ItemCount = p.Items.Count,
            RemainingItemCount = p.Items.Count(i => i.Status != ItemStatuses.Sold),
            SoldItemCount = p.Items.Count(i => i.Status == ItemStatuses.Sold)
        }).ToList();
    }

    public async Task<PurchaseDetailDto> UpdateAsync(Guid id, UpdatePurchaseFullRequest request, CancellationToken ct = default)
    {
        ValidateHeader(request);
        var allocation = Allocate(request);
        if (!allocation.IsReadyToSave && !request.AllowDifference)
            throw new ValidationFailedException(allocation.ValidationErrors);

        await using var db = _dbContextFactory.CreateForCurrentTenant();
        var username = _currentUser.DisplayName;

        var purchase = await db.Purchases
            .Include(p => p.ItemLines)
            .Include(p => p.ExpenseLines)
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("PURCHASE_NOT_FOUND", "Purchase was not found.");

        var auditEntries = new List<AuditEntry> { new("Purchase", purchase.Id, "Updated", username, "manual") };

        purchase.SourceName = await ResolveSourceNameAsync(db, request.SourceName, request.SupplierId, ct);
        purchase.PurchaseDate = request.PurchaseDate;
        purchase.PaymentMethod = request.PaymentMethod;
        purchase.Comment = request.Comment;
        purchase.PurchaseType = request.PurchaseType;
        purchase.UsedResellerPermit = request.PurchaseType == "ResellerPermit";
        ApplyFullWorkflowFields(purchase, request, allocation);
        purchase.UpdatedBy = username;
        purchase.Touch();

        // Reconcile lines: remove lines dropped from the request, update
        // matched lines (reconciling their physical Item count against the
        // new Quantity), add brand-new lines. Every removal/reduction is
        // gated on SafelyRemovableStatuses — see the class doc-comment.
        var requestLineIds = request.ItemLines.Where(l => l.Id is not null).Select(l => l.Id!.Value).ToHashSet();

        foreach (var removedLine in purchase.ItemLines.Where(l => !requestLineIds.Contains(l.Id)).ToList())
        {
            var lineItems = purchase.Items.Where(it => it.PurchaseItemLineId == removedLine.Id).ToList();
            BlockIfAnyNotSafelyRemovable(lineItems, $"удалить строку «{removedLine.ItemName}»");

            foreach (var item in lineItems)
            {
                item.SoftDelete();
                auditEntries.Add(new AuditEntry("Item", item.Id, "Deleted", username, "manual"));
            }
            removedLine.SoftDelete();
            auditEntries.Add(new AuditEntry("PurchaseItemLine", removedLine.Id, "Deleted", username, "manual"));
        }

        var existingLinesById = purchase.ItemLines.ToDictionary(l => l.Id);

        for (var i = 0; i < request.ItemLines.Count; i++)
        {
            var input = request.ItemLines[i];
            var calc = allocation.Lines[i];

            PurchaseItemLine line;
            List<Item> lineItems;
            if (input.Id is { } lineId && existingLinesById.TryGetValue(lineId, out var existing))
            {
                line = existing;
                lineItems = purchase.Items.Where(it => it.PurchaseItemLineId == lineId && it.DeletedAt is null)
                    .OrderBy(it => it.ItemNumber).ToList();
            }
            else
            {
                line = PurchaseItemLine.CreateNew(purchase.Id, calc.LineNumber, input.ItemName, input.CategoryName,
                    input.Quantity, input.UnitPurchaseCost, input.Notes);
                line.CreatedBy = username;
                db.PurchaseItemLines.Add(line);
                auditEntries.Add(new AuditEntry("PurchaseItemLine", line.Id, "Created", username, "manual"));
                lineItems = new List<Item>();
            }

            line.LineNumber = calc.LineNumber;
            line.ItemName = input.ItemName;
            line.CategoryName = input.CategoryName;
            line.Quantity = input.Quantity;
            line.UnitPurchaseCost = input.UnitPurchaseCost;
            line.Notes = input.Notes;
            ApplyLineAllocation(line, calc, input);
            line.UpdatedBy = username;
            line.Touch();

            if (lineItems.Count > input.Quantity)
            {
                var excess = lineItems.OrderByDescending(it => it.ItemNumber).Take(lineItems.Count - input.Quantity).ToList();
                BlockIfAnyNotSafelyRemovable(excess, $"уменьшить количество в строке «{line.ItemName}»");

                foreach (var it in excess)
                {
                    it.SoftDelete();
                    auditEntries.Add(new AuditEntry("Item", it.Id, "Deleted", username, "manual"));
                }
                lineItems = lineItems.Except(excess).ToList();
            }
            else if (lineItems.Count < input.Quantity)
            {
                for (var n = lineItems.Count; n < input.Quantity; n++)
                {
                    var newItem = Item.CreateNew(purchase.Id, input.ItemName, input.CategoryName, 0m, input.Notes);
                    newItem.PurchaseItemLineId = line.Id;
                    db.Items.Add(newItem);
                    lineItems.Add(newItem);
                    auditEntries.Add(new AuditEntry("Item", newItem.Id, "Created", username, "manual"));
                }
            }

            // Re-run the within-line split across the (possibly changed)
            // surviving set. Brand-new Items have no ItemNumber yet
            // (assigned on SaveChangesAsync), so they sort after existing
            // ones and get a stable tie-break by Id.
            var orderedForCostBasis = lineItems
                .OrderBy(it => it.ItemNumber == 0 ? long.MaxValue : it.ItemNumber)
                .ThenBy(it => it.Id)
                .ToList();
            for (var u = 0; u < orderedForCostBasis.Count && u < calc.UnitCostBases.Count; u++)
            {
                orderedForCostBasis[u].Name = input.ItemName;
                orderedForCostBasis[u].CategoryName = input.CategoryName;
                if (orderedForCostBasis[u].CostBasisCalculated != calc.UnitCostBases[u])
                {
                    auditEntries.Add(new AuditEntry("Item", orderedForCostBasis[u].Id, "CostBasisRecalculated", username, "manual",
                        "CostBasisCalculated", orderedForCostBasis[u].CostBasisCalculated.ToString(), calc.UnitCostBases[u].ToString()));
                }
                orderedForCostBasis[u].SetCalculatedCostBasis(calc.UnitCostBases[u]);
                orderedForCostBasis[u].Touch();
            }
        }

        // Fully replace the expense line set — no downstream FK references
        // it, so diffing brings no benefit (see PurchaseExpenseLine's doc-comment).
        db.PurchaseExpenseLines.RemoveRange(purchase.ExpenseLines);
        foreach (var expenseInput in request.ExpenseLines)
        {
            var expenseLine = PurchaseExpenseLine.CreateNew(purchase.Id, expenseInput.ExpenseType, expenseInput.Amount, expenseInput.Notes);
            expenseLine.CreatedBy = username;
            expenseLine.UpdatedBy = username;
            db.PurchaseExpenseLines.Add(expenseLine);
        }

        await db.SaveChangesAsync(ct);
        await _auditLogger.LogManyAsync(auditEntries, ct);

        return await GetAsync(purchase.Id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = _dbContextFactory.CreateForCurrentTenant();

        var purchase = await db.Purchases.Include(p => p.Items).FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("PURCHASE_NOT_FOUND", "Purchase was not found.");

        var blocking = purchase.Items.Where(i => i.Status is ItemStatuses.Sold or ItemStatuses.Listed).ToList();
        if (blocking.Count > 0)
        {
            var numbers = string.Join(", ", blocking.Select(i => i.ItemNumber));
            throw new ConflictException("PURCHASE_HAS_ACTIVE_ITEMS",
                $"Cannot delete this Purchase: Item(s) {numbers} are already Sold or Listed and must be handled first.");
        }

        foreach (var item in purchase.Items) item.SoftDelete();
        purchase.SoftDelete();

        await db.SaveChangesAsync(ct);
        await _auditLogger.LogAsync(new AuditEntry("Purchase", purchase.Id, "Deleted", _currentUser.DisplayName, "manual"), ct);
    }

    public PurchaseAllocationResult PreviewAllocation(PurchaseAllocationPreviewRequest request) =>
        PurchaseAllocationCalculator.Calculate(
            request.ItemLines, request.ExpenseLines, request.TaxableAmount, request.SalesTaxRate,
            request.SalesTaxAmountOverride, request.SalesTaxAllocationMethod, request.ExpenseAllocationMethod,
            request.ManualAdjustment);

    private static void BlockIfAnyNotSafelyRemovable(IReadOnlyList<Item> items, string actionDescription)
    {
        var blocking = items.Where(i => !SafelyRemovableStatuses.Contains(i.Status)).ToList();
        if (blocking.Count == 0) return;

        var numbers = string.Join(", ", blocking.Select(i => i.ItemNumber));
        throw new ConflictException("PURCHASE_LINE_HAS_ACTIVE_ITEMS",
            $"Cannot {actionDescription}: Item(s) {numbers} are already Listed/Sold and must be handled first.");
    }

    private static void ValidateHeader(CreatePurchaseFullRequest request)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(request.SourceName)) errors.Add("Source name is required.");
        if (string.IsNullOrWhiteSpace(request.PurchaseType)) errors.Add("Purchase type is required.");
        if (errors.Count > 0) throw new ValidationFailedException(errors);
    }

    private static PurchaseAllocationResult Allocate(CreatePurchaseFullRequest request) =>
        PurchaseAllocationCalculator.Calculate(
            request.ItemLines, request.ExpenseLines, request.TaxableAmount, request.SalesTaxRate,
            request.SalesTaxAmountOverride, request.SalesTaxAllocationMethod, request.ExpenseAllocationMethod,
            request.ManualAdjustment);

    private static async Task<string> ResolveSourceNameAsync(InventoryDbContext db, string sourceName, Guid? supplierId, CancellationToken ct)
    {
        if (supplierId is not { } id) return sourceName.Trim();

        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NotFoundException("SUPPLIER_NOT_FOUND", "Supplier was not found.");
        return supplier.Name; // denormalized snapshot, same convention as 0003_suppliers.sql
    }

    private static void ApplyFullWorkflowFields(Purchase purchase, CreatePurchaseFullRequest request, PurchaseAllocationResult allocation)
    {
        purchase.SupplierId = request.SupplierId;
        purchase.SourceType = request.SourceType;
        purchase.MerchandiseSubtotal = allocation.MerchandiseSubtotal;
        purchase.TaxableAmount = allocation.TaxableAmount;
        purchase.SalesTaxAmount = allocation.SalesTaxAmount;
        purchase.SalesTaxRate = request.SalesTaxRate;
        purchase.SalesTaxAmountCalculated = request.SalesTaxRate is { } rate
            ? Math.Round(allocation.TaxableAmount * rate / 100m, 2, MidpointRounding.AwayFromZero)
            : null;
        purchase.SalesTaxIsManualOverride = request.SalesTaxAmountOverride is not null;
        purchase.SalesTaxAllocationMethod = request.SalesTaxAllocationMethod;
        purchase.ExpenseAllocationMethod = request.ExpenseAllocationMethod;
        purchase.ManualAdjustment = request.ManualAdjustment;
        purchase.PermitNumber = request.PermitNumber;
        purchase.PermitDate = request.PermitDate;
        purchase.TaxExemptAmount = request.TaxExemptAmount;
        purchase.TotalAmount = allocation.TotalPurchaseCost;
    }

    private static void ApplyLineAllocation(PurchaseItemLine line, PurchaseAllocationLineResultDto calc, PurchaseItemLineInput input)
    {
        line.LinePurchaseCost = calc.LinePurchaseCost;
        line.AllocatedSalesTax = calc.AllocatedSalesTax;
        line.ManualAllocatedSalesTax = input.ManualAllocatedSalesTax;
        line.AllocatedExpenses = calc.AllocatedExpenses;
        line.ManualAllocatedExpenses = input.ManualAllocatedExpenses;
        line.FinalLineCostBasis = calc.FinalLineCostBasis;
    }

    private static PurchaseDetailDto ToDetailDto(Purchase p)
    {
        var itemsByLine = p.Items
            .Where(i => i.PurchaseItemLineId is not null)
            .GroupBy(i => i.PurchaseItemLineId!.Value)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<PurchaseLineItemRefDto>)g.OrderBy(i => i.ItemNumber)
                .Select(i => new PurchaseLineItemRefDto { Id = i.Id, ItemNumber = i.ItemNumber })
                .ToList());

        var lineDtos = p.ItemLines
            .OrderBy(l => l.LineNumber)
            .Select(l => new PurchaseItemLineDto
            {
                Id = l.Id,
                LineNumber = l.LineNumber,
                ItemName = l.ItemName,
                CategoryName = l.CategoryName,
                Quantity = l.Quantity,
                UnitPurchaseCost = l.UnitPurchaseCost,
                LinePurchaseCost = l.LinePurchaseCost,
                AllocatedSalesTax = l.AllocatedSalesTax,
                ManualAllocatedSalesTax = l.ManualAllocatedSalesTax,
                AllocatedExpenses = l.AllocatedExpenses,
                ManualAllocatedExpenses = l.ManualAllocatedExpenses,
                FinalLineCostBasis = l.FinalLineCostBasis,
                Notes = l.Notes,
                CreatedItems = itemsByLine.TryGetValue(l.Id, out var refs) ? refs : Array.Empty<PurchaseLineItemRefDto>()
            })
            .ToList();

        var expenseDtos = p.ExpenseLines.Select(e => new PurchaseExpenseLineDto
        {
            Id = e.Id,
            ExpenseType = e.ExpenseType,
            Amount = e.Amount,
            Notes = e.Notes
        }).ToList();

        var totalItemCount = p.Items.Count;
        var soldItemCount = p.Items.Count(i => i.Status == ItemStatuses.Sold);

        return new PurchaseDetailDto
        {
            Id = p.Id,
            PurchaseDate = p.PurchaseDate,
            SourceName = p.SourceName,
            SupplierId = p.SupplierId,
            SourceType = p.SourceType,
            PurchaseType = p.PurchaseType,
            UsedResellerPermit = p.UsedResellerPermit,
            PermitNumber = p.PermitNumber,
            PermitDate = p.PermitDate,
            TaxExemptAmount = p.TaxExemptAmount,
            PaymentMethod = p.PaymentMethod,
            Comment = p.Comment,
            MerchandiseSubtotal = p.MerchandiseSubtotal,
            TaxableAmount = p.TaxableAmount,
            SalesTaxRate = p.SalesTaxRate,
            SalesTaxAmount = p.SalesTaxAmount,
            SalesTaxAmountCalculated = p.SalesTaxAmountCalculated,
            SalesTaxIsManualOverride = p.SalesTaxIsManualOverride,
            SalesTaxAllocationMethod = p.SalesTaxAllocationMethod,
            ExpenseAllocationMethod = p.ExpenseAllocationMethod,
            ManualAdjustment = p.ManualAdjustment,
            TotalAmount = p.TotalAmount,
            ItemLines = lineDtos,
            ExpenseLines = expenseDtos,
            TotalItemCount = totalItemCount,
            SoldItemCount = soldItemCount,
            RemainingItemCount = totalItemCount - soldItemCount,
            CreatedAt = p.CreatedAt,
            CreatedBy = p.CreatedBy,
            UpdatedAt = p.UpdatedAt,
            UpdatedBy = p.UpdatedBy
        };
    }
}
