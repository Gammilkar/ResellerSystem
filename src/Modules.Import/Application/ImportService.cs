using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using ResellerSystem.Domain.Shared.Dto;
using ResellerSystem.Modules.Expenses.Application;
using ResellerSystem.Modules.Import.Data;
using ResellerSystem.Modules.Import.Domain;
using ResellerSystem.Modules.Inventory.Application;
using ResellerSystem.Modules.Sales.Application;
using ResellerSystem.Server.Application.Audit;
using ResellerSystem.Server.Application.Exceptions;
using ResellerSystem.Server.Domain.Abstractions;

namespace ResellerSystem.Modules.Import.Application;

/// <summary>
/// Product Specification sections 57-61: XLSX import with user-driven
/// column mapping, covering the full Purchase/Item/Listing/Sale/Fees/
/// Return/Expenses field set. One staged row = one Item; Purchase rows
/// sharing the same "purchase.groupKey" mapped value are collapsed into a
/// single Purchase (see ConfirmAsync). Nothing is written to the business
/// tables until ConfirmAsync — Upload only ever writes to this module's
/// own staging tables (Architecture Plan v0.1 section 40).
///
/// CSV import (the original Stage 1 format) is kept working through the
/// same pipeline with an implied fixed mapping — see UploadCsvAsync.
/// </summary>
public interface IImportService
{
    IReadOnlyList<ImportTargetFieldDto> GetTargetFields();

    Task<InspectXlsxResultDto> InspectXlsxAsync(Stream content, CancellationToken ct = default);
    Task<ImportBatchDto> UploadXlsxAsync(Stream content, string filename, IReadOnlyDictionary<string, string> mapping, CancellationToken ct = default);
    Task<ImportBatchDto> UploadCsvAsync(Stream content, string filename, CancellationToken ct = default);

    Task<ImportBatchDto> GetBatchAsync(Guid batchId, CancellationToken ct = default);
    Task<ConfirmImportResultDto> ConfirmAsync(Guid batchId, CancellationToken ct = default);

    Task<IReadOnlyList<ImportMappingTemplateDto>> ListMappingTemplatesAsync(string importType, CancellationToken ct = default);
    Task<ImportMappingTemplateDto> SaveMappingTemplateAsync(SaveMappingTemplateRequest request, CancellationToken ct = default);
}

public sealed class ImportService : IImportService
{
    private static readonly string[] RequiredFields = { "purchase.sourceName", "purchase.date", "item.name", "item.purchasePrice" };

    private readonly IImportDbContextFactory _dbContextFactory;
    private readonly IInventoryService _inventoryService;
    private readonly ISalesService _salesService;
    private readonly IExpensesService _expensesService;
    private readonly IAuditLogger _auditLogger;
    private readonly ICurrentUserContext _currentUser;

    public ImportService(
        IImportDbContextFactory dbContextFactory,
        IInventoryService inventoryService,
        ISalesService salesService,
        IExpensesService expensesService,
        IAuditLogger auditLogger,
        ICurrentUserContext currentUser)
    {
        _dbContextFactory = dbContextFactory;
        _inventoryService = inventoryService;
        _salesService = salesService;
        _expensesService = expensesService;
        _auditLogger = auditLogger;
        _currentUser = currentUser;
    }

    public IReadOnlyList<ImportTargetFieldDto> GetTargetFields() => ImportTargetFields.All;

    public Task<InspectXlsxResultDto> InspectXlsxAsync(Stream content, CancellationToken ct = default)
    {
        using var workbook = new XLWorkbook(content);
        var sheet = workbook.Worksheets.First();
        var headerRow = sheet.FirstRowUsed() ?? throw new ValidationFailedException(new[] { "File is empty." });

        var columns = headerRow.CellsUsed()
            .Select(c => c.GetString().Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        if (columns.Count == 0)
        {
            throw new ValidationFailedException(new[] { "No column headers found in the first row." });
        }

        return Task.FromResult(new InspectXlsxResultDto { Columns = columns });
    }

    public async Task<ImportBatchDto> UploadXlsxAsync(Stream content, string filename, IReadOnlyDictionary<string, string> mapping, CancellationToken ct = default)
    {
        var missingRequired = RequiredFields.Where(f => !mapping.ContainsKey(f) || string.IsNullOrWhiteSpace(mapping[f])).ToList();
        if (missingRequired.Count > 0)
        {
            throw new ValidationFailedException(new[] { $"These required fields are not mapped to a column: {string.Join(", ", missingRequired)}." });
        }

        using var workbook = new XLWorkbook(content);
        var sheet = workbook.Worksheets.First();
        var headerRow = sheet.FirstRowUsed() ?? throw new ValidationFailedException(new[] { "File is empty." });

        var columnNames = new Dictionary<int, string>();
        foreach (var cell in headerRow.CellsUsed())
        {
            var name = cell.GetString().Trim();
            if (!string.IsNullOrWhiteSpace(name)) columnNames[cell.Address.ColumnNumber] = name;
        }

        var rawRows = new List<Dictionary<string, string>>();
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? headerRow.RowNumber();
        for (var rowNum = headerRow.RowNumber() + 1; rowNum <= lastRow; rowNum++)
        {
            var row = sheet.Row(rowNum);
            if (row.IsEmpty()) continue;

            var raw = new Dictionary<string, string>();
            foreach (var (colNum, colName) in columnNames)
            {
                var cell = row.Cell(colNum);
                var text = cell.DataType == XLDataType.DateTime
                    ? cell.GetDateTime().ToString("yyyy-MM-dd")
                    : cell.GetString().Trim();
                if (!string.IsNullOrEmpty(text)) raw[colName] = text;
            }
            if (raw.Count > 0) rawRows.Add(raw);
        }

        return await BuildBatchAsync(filename, "xlsx-full", mapping, rawRows, ct);
    }

    /// <summary>Legacy Stage 1 shape: SourceName,ItemName,TotalAmount,PurchaseDate
    /// (Quantity is no longer expanded into repeated items — a CSV/XLSX
    /// row now always means exactly one Item, matching xlsx-full and
    /// Product Specification section 29).</summary>
    public async Task<ImportBatchDto> UploadCsvAsync(Stream content, string filename, CancellationToken ct = default)
    {
        using var reader = new StreamReader(content);
        var headerLine = await reader.ReadLineAsync(ct);
        if (headerLine is null)
        {
            throw new ValidationFailedException(new[] { "File is empty." });
        }

        var headers = headerLine.Split(',').Select(h => h.Trim()).ToList();
        var requiredColumns = new[] { "SourceName", "ItemName", "TotalAmount" };
        var missing = requiredColumns.Where(c => !headers.Contains(c, StringComparer.OrdinalIgnoreCase)).ToList();
        if (missing.Count > 0)
        {
            throw new ValidationFailedException(new[] { $"Missing required column(s): {string.Join(", ", missing)}." });
        }

        var mapping = new Dictionary<string, string>
        {
            ["purchase.sourceName"] = "SourceName",
            ["item.name"] = "ItemName",
            ["item.purchasePrice"] = "TotalAmount",
            ["purchase.date"] = "PurchaseDate"
        };

        var rawRows = new List<Dictionary<string, string>>();
        string? line;
        while ((line = await reader.ReadLineAsync(ct)) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var values = line.Split(',').Select(v => v.Trim()).ToList();
            var raw = headers.Zip(values, (h, v) => (h, v))
                .Where(x => !string.IsNullOrEmpty(x.v))
                .ToDictionary(x => x.h, x => x.v);
            if (raw.Count > 0) rawRows.Add(raw);
        }

        return await BuildBatchAsync(filename, "csv-purchases", mapping, rawRows, ct);
    }

    private async Task<ImportBatchDto> BuildBatchAsync(
        string filename, string importType, IReadOnlyDictionary<string, string> mapping,
        IReadOnlyList<Dictionary<string, string>> rawRows, CancellationToken ct)
    {
        await using var db = _dbContextFactory.CreateForCurrentTenant();

        var mappingJson = JsonSerializer.Serialize(mapping);
        var batch = ImportBatch.CreateNew(filename, importType, mappingJson);
        db.ImportBatches.Add(batch);

        // Duplicate detection (Product Specification section 61): match
        // against Order ID / Transaction ID / External Listing ID already
        // present in the tenant database, not just within this file.
        var (existingOrderIds, existingTransactionIds) = await ExistingSaleKeysAsync(ct);
        var existingListingIds = await ExistingListingKeysAsync(ct);
        var seenInBatch = new HashSet<string>();

        var rowIndex = 0;
        foreach (var raw in rawRows)
        {
            rowIndex++;
            var mapped = Resolve(raw, mapping);
            var errors = new List<string>();

            if (mapped.GetOrNull("purchase.sourceName") is null) errors.Add("purchase.sourceName is required.");
            if (mapped.GetOrNull("item.name") is null) errors.Add("item.name is required.");

            var dateText = mapped.GetOrNull("purchase.date");
            if (dateText is null) errors.Add("purchase.date is required.");
            else if (!ImportParsing.TryParseDate(dateText, out _)) errors.Add($"purchase.date '{dateText}' is not a valid date.");

            var priceText = mapped.GetOrNull("item.purchasePrice");
            if (priceText is null) errors.Add("item.purchasePrice is required.");
            else if (!ImportParsing.TryParseDecimal(priceText, out var price) || price < 0) errors.Add($"item.purchasePrice '{priceText}' is not a valid non-negative number.");

            var stagingRow = ImportStagingRow.CreateNew(batch.Id, rowIndex, JsonSerializer.Serialize(raw));

            var orderId = mapped.GetOrNull("sale.orderId");
            var transactionId = mapped.GetOrNull("sale.transactionId");
            var externalListingId = mapped.GetOrNull("listing.externalListingId");

            var dedupeKey = orderId is not null || transactionId is not null
                ? $"order:{orderId}|txn:{transactionId}"
                : externalListingId is not null ? $"listing:{externalListingId}" : null;

            var isDuplicate = false;
            if (dedupeKey is not null)
            {
                if (!seenInBatch.Add(dedupeKey)) isDuplicate = true;
                if ((orderId is not null && existingOrderIds.Contains(orderId))
                    || (transactionId is not null && existingTransactionIds.Contains(transactionId))
                    || (externalListingId is not null && existingListingIds.Contains(externalListingId)))
                {
                    isDuplicate = true;
                }
            }

            stagingRow.ValidationErrors = JsonSerializer.Serialize(errors);
            stagingRow.IsValid = errors.Count == 0;
            stagingRow.PossibleDuplicate = isDuplicate;

            db.ImportStagingRows.Add(stagingRow);
        }

        await db.SaveChangesAsync(ct);
        return await GetBatchAsync(batch.Id, ct);
    }

    public async Task<ImportBatchDto> GetBatchAsync(Guid batchId, CancellationToken ct = default)
    {
        await using var db = _dbContextFactory.CreateForCurrentTenant();
        var batch = await db.ImportBatches.Include(b => b.Rows).FirstOrDefaultAsync(b => b.Id == batchId, ct)
            ?? throw new NotFoundException("IMPORT_BATCH_NOT_FOUND", "Import batch was not found.");

        return ToDto(batch);
    }

    public async Task<ConfirmImportResultDto> ConfirmAsync(Guid batchId, CancellationToken ct = default)
    {
        await using var db = _dbContextFactory.CreateForCurrentTenant();
        var batch = await db.ImportBatches.Include(b => b.Rows).FirstOrDefaultAsync(b => b.Id == batchId, ct)
            ?? throw new NotFoundException("IMPORT_BATCH_NOT_FOUND", "Import batch was not found.");

        if (batch.Status != "Staged")
        {
            throw new ConflictException("IMPORT_ALREADY_PROCESSED", $"Import batch is already '{batch.Status}'.");
        }

        var mapping = JsonSerializer.Deserialize<Dictionary<string, string>>(batch.ColumnMapping) ?? new();

        var purchaseIdByGroupKey = new Dictionary<string, Guid>();
        int purchaseCount = 0, itemCount = 0, listingCount = 0, saleCount = 0, returnCount = 0;

        foreach (var row in batch.Rows.Where(r => r.IsValid && !r.PossibleDuplicate).OrderBy(r => r.RowIndex))
        {
            var raw = JsonSerializer.Deserialize<Dictionary<string, string>>(row.RawData) ?? new();
            var mapped = Resolve(raw, mapping);

            // --- Purchase (grouped by purchase.groupKey when given) ---
            var groupKey = mapped.GetOrNull("purchase.groupKey") ?? $"row:{row.Id}";
            var isNewPurchase = !purchaseIdByGroupKey.TryGetValue(groupKey, out var purchaseId);
            if (isNewPurchase)
            {
                ImportParsing.TryParseDecimal(mapped.GetOrNull("purchase.salesTaxAmount"), out var taxAmount);
                decimal? taxRate = ImportParsing.TryParseDecimal(mapped.GetOrNull("purchase.salesTaxRate"), out var tr) ? tr : null;

                var purchase = await _inventoryService.CreatePurchaseHeaderAsync(new CreatePurchaseHeaderRequest
                {
                    PurchaseDate = ImportParsing.TryParseDate(mapped.GetOrNull("purchase.date"), out var pd) ? pd : DateOnly.FromDateTime(DateTime.Today),
                    SourceName = mapped.GetOrNull("purchase.sourceName")!,
                    TotalAmount = 0, // informational only for header-only purchases — real cost lives on each Item
                    SalesTaxAmount = taxAmount,
                    SalesTaxRate = taxRate,
                    PaymentMethod = mapped.GetOrNull("purchase.paymentMethod"),
                    PurchaseType = mapped.GetOrNull("purchase.type") is { } pt && pt is "TaxPaid" or "ResellerPermit" or "NoTax" ? pt : "TaxPaid",
                    Comment = mapped.GetOrNull("purchase.comment")
                }, ct);

                purchaseId = purchase.Id;
                purchaseIdByGroupKey[groupKey] = purchaseId;
                purchaseCount++;

                if (ImportParsing.TryParseDecimal(mapped.GetOrNull("purchase.additionalExpenses"), out var addlExpense) && addlExpense > 0)
                {
                    await _expensesService.CreateAsync(new CreateExpenseRequest
                    {
                        ExpenseType = "PurchaseFee",
                        Amount = addlExpense,
                        ExpenseDate = pd,
                        PurchaseId = purchaseId
                    }, ct);
                }
            }

            // --- Item ---
            ImportParsing.TryParseDecimal(mapped.GetOrNull("item.purchasePrice"), out var itemPrice);
            var item = await _inventoryService.AddItemToPurchaseAsync(purchaseId, new AddItemToPurchaseRequest
            {
                Name = mapped.GetOrNull("item.name")!,
                CategoryName = mapped.GetOrNull("item.category"),
                CostBasis = itemPrice,
                Notes = mapped.GetOrNull("item.notes")
            }, ct);
            itemCount++;

            var updateFields = new UpdateItemRequest
            {
                Status = mapped.GetOrNull("item.status"),
                CostBasisOverride = ImportParsing.TryParseDecimal(mapped.GetOrNull("item.costBasisOverride"), out var cbo) ? cbo : null
            };
            if (updateFields.Status is not null || updateFields.CostBasisOverride is not null)
            {
                await _inventoryService.UpdateItemAsync(item.Id, updateFields, ct);
            }

            // --- Listing ---
            Guid? listingId = null;
            var listingMarketplace = mapped.GetOrNull("listing.marketplace");
            if (listingMarketplace is not null)
            {
                var listing = await _salesService.CreateListingAsync(new CreateListingRequest
                {
                    ItemId = item.Id,
                    Marketplace = listingMarketplace,
                    MarketplaceAccount = mapped.GetOrNull("listing.marketplaceAccount"),
                    ExternalListingId = mapped.GetOrNull("listing.externalListingId"),
                    PublishedDate = ImportParsing.TryParseDate(mapped.GetOrNull("listing.publishedDate"), out var pubDate) ? pubDate : null,
                    ListingPrice = ImportParsing.TryParseDecimal(mapped.GetOrNull("listing.listingPrice"), out var lp) ? lp : null,
                    Promoted = ImportParsing.ParseBool(mapped.GetOrNull("listing.promoted")),
                    PromotedRate = ImportParsing.TryParseDecimal(mapped.GetOrNull("listing.promotedRate"), out var pr) ? pr : null,
                    Url = mapped.GetOrNull("listing.url"),
                    EndDate = ImportParsing.TryParseDate(mapped.GetOrNull("listing.endDate"), out var ed) ? ed : null
                }, ct);
                listingId = listing.Id;
                listingCount++;
            }

            // --- Sale ---
            Guid? saleId = null;
            var saleMarketplace = mapped.GetOrNull("sale.marketplace") ?? listingMarketplace;
            var saleItemPriceText = mapped.GetOrNull("sale.itemSalePrice");
            if (saleMarketplace is not null && saleItemPriceText is not null)
            {
                ImportParsing.TryParseDecimal(saleItemPriceText, out var saleItemPrice);
                ImportParsing.TryParseDecimal(mapped.GetOrNull("sale.buyerPaidShipping"), out var shipping);
                ImportParsing.TryParseDecimal(mapped.GetOrNull("sale.buyerPaidSalesTax"), out var buyerTax);
                ImportParsing.TryParseDecimal(mapped.GetOrNull("sale.marketplaceCollectedTax"), out var mpTax);
                var hasPayout = ImportParsing.TryParseDecimal(mapped.GetOrNull("sale.payoutAmount"), out var payout);

                var sale = await _salesService.CreateSaleAsync(new CreateSaleRequest
                {
                    ItemId = item.Id,
                    ListingId = listingId,
                    Marketplace = saleMarketplace,
                    MarketplaceAccount = mapped.GetOrNull("sale.marketplaceAccount"),
                    OrderId = mapped.GetOrNull("sale.orderId"),
                    TransactionId = mapped.GetOrNull("sale.transactionId"),
                    SaleDate = ImportParsing.TryParseDate(mapped.GetOrNull("sale.saleDate"), out var saleDate) ? saleDate : DateOnly.FromDateTime(DateTime.Today),
                    ItemSalePrice = saleItemPrice,
                    BuyerPaidShipping = shipping,
                    BuyerPaidSalesTax = buyerTax,
                    MarketplaceCollectedTax = mpTax,
                    // Payout is a distinct real-world figure (Product Specification
                    // section 42) — only defaulted here when the sheet genuinely
                    // doesn't have it, so a real value is never silently overwritten.
                    PayoutAmount = hasPayout ? payout : saleItemPrice + shipping,
                    PaymentMethod = mapped.GetOrNull("sale.paymentMethod"),
                    DestinationState = mapped.GetOrNull("sale.destinationState"),
                    DestinationZip = mapped.GetOrNull("sale.destinationZip")
                }, ct);
                saleId = sale.Id;
                saleCount++;

                await AddFeeIfPresentAsync(sale.Id, "FinalValueFee", mapped.GetOrNull("fee.finalValueFee"), mapped.GetOrNull("fee.finalValueFeeRate"), ct);
                await AddFeeIfPresentAsync(sale.Id, "PerOrderFee", mapped.GetOrNull("fee.perOrderFee"), null, ct);
                await AddFeeIfPresentAsync(sale.Id, "InsertionFee", mapped.GetOrNull("fee.insertionFee"), null, ct);
                await AddFeeIfPresentAsync(sale.Id, "ListingUpgradeFee", mapped.GetOrNull("fee.listingUpgradeFee"), null, ct);
                await AddFeeIfPresentAsync(sale.Id, "PromotedListingsFee", mapped.GetOrNull("fee.promotedListingsFee"), null, ct);
                await AddFeeIfPresentAsync(sale.Id, "InternationalFee", mapped.GetOrNull("fee.internationalFee"), null, ct);
                await AddFeeIfPresentAsync(sale.Id, "TaxOnSellerFees", mapped.GetOrNull("fee.taxOnSellerFees"), null, ct);
                await AddFeeIfPresentAsync(sale.Id, "FeeCredit", mapped.GetOrNull("fee.feeCredit"), null, ct);
                await AddFeeIfPresentAsync(sale.Id, "DisputeFee", mapped.GetOrNull("fee.disputeFee"), null, ct);
                await AddFeeIfPresentAsync(sale.Id, "ChargebackFee", mapped.GetOrNull("fee.chargebackFee"), null, ct);
                await AddFeeIfPresentAsync(sale.Id, "Other", mapped.GetOrNull("fee.otherMarketplaceFees"), null, ct);

                await AddExpenseIfPresentAsync("ShippingLabel", mapped.GetOrNull("expense.shippingLabel"), saleDate, saleId.Value, ct);
                await AddExpenseIfPresentAsync("Packaging", mapped.GetOrNull("expense.packaging"), saleDate, saleId.Value, ct);
                await AddExpenseIfPresentAsync("Insurance", mapped.GetOrNull("expense.insurance"), saleDate, saleId.Value, ct);
                await AddExpenseIfPresentAsync("Other", mapped.GetOrNull("expense.other"), saleDate, saleId.Value, ct);

                // --- Return ---
                var returnDateText = mapped.GetOrNull("return.date");
                var refundText = mapped.GetOrNull("return.refundAmount");
                if (returnDateText is not null || refundText is not null)
                {
                    ImportParsing.TryParseDecimal(refundText, out var refund);
                    ImportParsing.TryParseDecimal(mapped.GetOrNull("return.refundedShipping"), out var refundShip);
                    ImportParsing.TryParseDecimal(mapped.GetOrNull("return.marketplaceFeeCredit"), out var retFeeCredit);
                    ImportParsing.TryParseDecimal(mapped.GetOrNull("return.shippingCost"), out var retShipCost);

                    await _salesService.CreateReturnAsync(new CreateReturnRequest
                    {
                        SaleId = saleId.Value,
                        ItemId = item.Id,
                        ReturnDate = ImportParsing.TryParseDate(returnDateText, out var retDate) ? retDate : saleDate,
                        ReturnType = mapped.GetOrNull("return.type") ?? "Full",
                        RefundToBuyer = refund,
                        RefundedShipping = refundShip,
                        MarketplaceFeeCredit = retFeeCredit,
                        ReturnShippingCost = retShipCost,
                        PhysicallyReturned = ImportParsing.ParseBool(mapped.GetOrNull("return.physicallyReturned")),
                        ConditionOnReturn = mapped.GetOrNull("return.conditionOnReturn")
                    }, ct);
                    returnCount++;

                    var statusAfter = mapped.GetOrNull("return.statusAfterReturn");
                    if (statusAfter is not null)
                    {
                        await _inventoryService.UpdateItemAsync(item.Id, new UpdateItemRequest { Status = statusAfter }, ct);
                    }
                }
            }
        }

        var skipped = batch.Rows.Count - itemCount;

        batch.Status = "Confirmed";
        batch.ConfirmedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        await _auditLogger.LogAsync(new AuditEntry(
            "ImportBatch", batch.Id, "Confirmed", _currentUser.DisplayName, "import",
            NewValue: $"{itemCount} item(s), {purchaseCount} purchase(s), {saleCount} sale(s)"), ct);

        return new ConfirmImportResultDto
        {
            CreatedPurchaseCount = purchaseCount,
            CreatedItemCount = itemCount,
            CreatedListingCount = listingCount,
            CreatedSaleCount = saleCount,
            CreatedReturnCount = returnCount,
            SkippedRowCount = skipped
        };
    }

    private async Task AddFeeIfPresentAsync(Guid saleId, string feeType, string? amountText, string? rateText, CancellationToken ct)
    {
        if (!ImportParsing.TryParseDecimal(amountText, out var amount) || amount == 0) return;
        decimal? rate = ImportParsing.TryParseDecimal(rateText, out var r) ? r : null;
        await _salesService.AddFeeAsync(saleId, new CreateSaleFeeRequest { FeeType = feeType, Amount = amount, Rate = rate }, ct);
    }

    private async Task AddExpenseIfPresentAsync(string expenseType, string? amountText, DateOnly date, Guid saleId, CancellationToken ct)
    {
        if (!ImportParsing.TryParseDecimal(amountText, out var amount) || amount == 0) return;
        await _expensesService.CreateAsync(new CreateExpenseRequest
        {
            ExpenseType = expenseType,
            Amount = amount,
            ExpenseDate = date,
            SaleId = saleId
        }, ct);
    }

    public async Task<IReadOnlyList<ImportMappingTemplateDto>> ListMappingTemplatesAsync(string importType, CancellationToken ct = default)
    {
        await using var db = _dbContextFactory.CreateForCurrentTenant();
        return await db.ImportMappingTemplates
            .Where(t => t.ImportType == importType)
            .OrderBy(t => t.Name)
            .Select(t => ToDto(t))
            .ToListAsync(ct);
    }

    public async Task<ImportMappingTemplateDto> SaveMappingTemplateAsync(SaveMappingTemplateRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationFailedException(new[] { "Template name is required." });

        await using var db = _dbContextFactory.CreateForCurrentTenant();

        var existing = await db.ImportMappingTemplates
            .FirstOrDefaultAsync(t => t.ImportType == request.ImportType && t.Name == request.Name.Trim(), ct);

        var mappingJson = JsonSerializer.Serialize(request.Mapping);
        if (existing is not null)
        {
            existing.Mapping = mappingJson;
            await db.SaveChangesAsync(ct);
            return ToDto(existing);
        }

        var template = ImportMappingTemplate.CreateNew(request.Name, request.ImportType, mappingJson);
        db.ImportMappingTemplates.Add(template);
        await db.SaveChangesAsync(ct);
        return ToDto(template);
    }

    private async Task<(HashSet<string> OrderIds, HashSet<string> TransactionIds)> ExistingSaleKeysAsync(CancellationToken ct)
    {
        var sales = await _salesService.ListSalesAsync(ct);
        var orderIds = new HashSet<string>();
        var transactionIds = new HashSet<string>();
        foreach (var s in sales)
        {
            if (!string.IsNullOrWhiteSpace(s.OrderId)) orderIds.Add(s.OrderId);
            if (!string.IsNullOrWhiteSpace(s.TransactionId)) transactionIds.Add(s.TransactionId);
        }
        return (orderIds, transactionIds);
    }

    private async Task<HashSet<string>> ExistingListingKeysAsync(CancellationToken ct)
    {
        var listings = await _salesService.ListListingsAsync(ct);
        return listings.Where(l => !string.IsNullOrWhiteSpace(l.ExternalListingId))
            .Select(l => l.ExternalListingId!)
            .ToHashSet();
    }

    private static Dictionary<string, string> Resolve(IReadOnlyDictionary<string, string> raw, IReadOnlyDictionary<string, string> mapping)
    {
        var result = new Dictionary<string, string>();
        foreach (var (targetKey, sourceColumn) in mapping)
        {
            if (string.IsNullOrWhiteSpace(sourceColumn)) continue;
            if (raw.TryGetValue(sourceColumn, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                result[targetKey] = value;
            }
        }
        return result;
    }

    private static ImportMappingTemplateDto ToDto(ImportMappingTemplate t) => new()
    {
        Id = t.Id,
        Name = t.Name,
        ImportType = t.ImportType,
        Mapping = JsonSerializer.Deserialize<Dictionary<string, string>>(t.Mapping) ?? new(),
        CreatedAt = t.CreatedAt
    };

    private static ImportBatchDto ToDto(ImportBatch batch)
    {
        var mapping = JsonSerializer.Deserialize<Dictionary<string, string>>(batch.ColumnMapping) ?? new();

        return new ImportBatchDto
        {
            Id = batch.Id,
            SourceFilename = batch.SourceFilename,
            ImportType = batch.ImportType,
            Status = batch.Status,
            RowCount = batch.Rows.Count,
            ValidRowCount = batch.Rows.Count(r => r.IsValid && !r.PossibleDuplicate),
            Rows = batch.Rows.OrderBy(r => r.RowIndex).Select(r =>
            {
                var raw = JsonSerializer.Deserialize<Dictionary<string, string>>(r.RawData) ?? new();
                return new ImportStagingRowDto
                {
                    Id = r.Id,
                    RowIndex = r.RowIndex,
                    RawData = raw,
                    MappedPreview = Resolve(raw, mapping),
                    ValidationErrors = JsonSerializer.Deserialize<List<string>>(r.ValidationErrors) ?? new List<string>(),
                    IsValid = r.IsValid,
                    PossibleDuplicate = r.PossibleDuplicate
                };
            }).ToList()
        };
    }
}
