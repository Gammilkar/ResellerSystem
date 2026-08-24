using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ResellerSystem.Domain.Shared.Dto;
using ResellerSystem.Modules.Import.Data;
using ResellerSystem.Modules.Import.Domain;
using ResellerSystem.Modules.Inventory.Application;
using ResellerSystem.Server.Application.Exceptions;

namespace ResellerSystem.Modules.Import.Application;

/// <summary>
/// CSV-only for this release (see KNOWN_LIMITATIONS.md "Import module
/// scope" — Excel/PDF importers, mapping templates, and eBay/marketplace
/// report parsers are NOT implemented). Expected header row:
///   SourceName,ItemName,TotalAmount,Quantity,PurchaseDate
/// Workflow strictly follows Architecture Plan v0.1 section 40:
/// Upload -> Parse -> Staging -> Preview -> Validation -> Confirm. Nothing
/// is written to the Inventory module's tables until ConfirmAsync is
/// explicitly called by the user after reviewing the staged preview.
/// </summary>
public interface IImportService
{
    Task<ImportBatchDto> UploadCsvAsync(Stream content, string filename, CancellationToken ct = default);
    Task<ImportBatchDto> GetBatchAsync(Guid batchId, CancellationToken ct = default);
    Task<ConfirmImportResultDto> ConfirmAsync(Guid batchId, CancellationToken ct = default);
}

public sealed class ImportService : IImportService
{
    private readonly IImportDbContextFactory _dbContextFactory;
    private readonly IInventoryService _inventoryService;

    public ImportService(IImportDbContextFactory dbContextFactory, IInventoryService inventoryService)
    {
        _dbContextFactory = dbContextFactory;
        _inventoryService = inventoryService;
    }

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

        await using var db = _dbContextFactory.CreateForCurrentTenant();
        var batch = ImportBatch.CreateNew(filename, "csv-purchases");
        db.ImportBatches.Add(batch);

        var existingOrderKeys = new HashSet<string>(); // simple in-batch duplicate check for this release
        var rowIndex = 0;
        string? line;
        while ((line = await reader.ReadLineAsync(ct)) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            rowIndex++;

            var values = line.Split(',').Select(v => v.Trim()).ToList();
            var rawDict = headers.Zip(values, (h, v) => (h, v)).ToDictionary(x => x.h, x => x.v);
            var rawJson = JsonSerializer.Serialize(rawDict);

            var stagingRow = ImportStagingRow.CreateNew(batch.Id, rowIndex, rawJson);

            var errors = new List<string>();

            var sourceName = rawDict.GetValueOrDefault("SourceName", "");
            var itemName = rawDict.GetValueOrDefault("ItemName", "");
            var totalAmountText = rawDict.GetValueOrDefault("TotalAmount", "");
            var quantityText = rawDict.GetValueOrDefault("Quantity", "1");
            var dateText = rawDict.GetValueOrDefault("PurchaseDate", "");

            if (string.IsNullOrWhiteSpace(sourceName)) errors.Add("SourceName is required.");
            if (string.IsNullOrWhiteSpace(itemName)) errors.Add("ItemName is required.");

            if (!decimal.TryParse(totalAmountText, out var totalAmount) || totalAmount < 0)
            {
                errors.Add($"TotalAmount '{totalAmountText}' is not a valid non-negative number.");
            }
            else
            {
                stagingRow.MappedTotalAmount = totalAmount;
            }

            if (!int.TryParse(quantityText, out var quantity) || quantity < 1)
            {
                errors.Add($"Quantity '{quantityText}' is not a valid positive integer.");
            }
            else
            {
                stagingRow.MappedQuantity = quantity;
            }

            if (!string.IsNullOrWhiteSpace(dateText) && DateOnly.TryParse(dateText, out var parsedDate))
            {
                stagingRow.MappedPurchaseDate = parsedDate;
            }
            else if (!string.IsNullOrWhiteSpace(dateText))
            {
                errors.Add($"PurchaseDate '{dateText}' is not a valid date.");
            }

            stagingRow.MappedSourceName = sourceName;
            stagingRow.MappedItemName = itemName;

            // Minimal duplicate check within this batch only (Architecture
            // Plan v0.1 section 43 asks for Order/Transaction ID matching —
            // this CSV format has no such column, so this only catches
            // literal duplicate rows within the same file upload).
            var dedupeKey = $"{sourceName}|{itemName}|{totalAmountText}|{dateText}";
            if (!existingOrderKeys.Add(dedupeKey))
            {
                stagingRow.PossibleDuplicate = true;
            }

            stagingRow.ValidationErrors = JsonSerializer.Serialize(errors);
            stagingRow.IsValid = errors.Count == 0;

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

        var created = 0;

        // User has already seen the staged preview (GetBatchAsync) before
        // calling this — Confirm only processes rows marked valid AND not
        // flagged as a possible duplicate, silently skipping the rest
        // rather than partially importing bad data.
        foreach (var row in batch.Rows.Where(r => r.IsValid && !r.PossibleDuplicate))
        {
            await _inventoryService.CreatePurchaseAsync(new CreatePurchaseRequest
            {
                PurchaseDate = row.MappedPurchaseDate ?? DateOnly.FromDateTime(DateTime.Today),
                SourceName = row.MappedSourceName!,
                TotalAmount = row.MappedTotalAmount!.Value,
                ItemName = row.MappedItemName!,
                Quantity = row.MappedQuantity ?? 1
            }, ct);
            created++;
        }
        var skipped = batch.Rows.Count - created;

        batch.Status = "Confirmed";
        batch.ConfirmedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        return new ConfirmImportResultDto { CreatedPurchaseCount = created, SkippedRowCount = skipped };
    }

    private static ImportBatchDto ToDto(ImportBatch batch) => new()
    {
        Id = batch.Id,
        SourceFilename = batch.SourceFilename,
        Status = batch.Status,
        RowCount = batch.Rows.Count,
        ValidRowCount = batch.Rows.Count(r => r.IsValid && !r.PossibleDuplicate),
        Rows = batch.Rows.OrderBy(r => r.RowIndex).Select(r => new ImportStagingRowDto
        {
            Id = r.Id,
            RowIndex = r.RowIndex,
            RawData = r.RawData,
            MappedSourceName = r.MappedSourceName,
            MappedItemName = r.MappedItemName,
            MappedTotalAmount = r.MappedTotalAmount,
            MappedQuantity = r.MappedQuantity,
            ValidationErrors = JsonSerializer.Deserialize<List<string>>(r.ValidationErrors) ?? new List<string>(),
            IsValid = r.IsValid,
            PossibleDuplicate = r.PossibleDuplicate
        }).ToList()
    };
}
