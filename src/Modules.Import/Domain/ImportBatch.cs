namespace ResellerSystem.Modules.Import.Domain;

public sealed class ImportBatch
{
    public Guid Id { get; private set; }
    public string SourceFilename { get; private set; } = string.Empty;
    public string ImportType { get; private set; } = "csv-purchases";
    public string Status { get; set; } = "Staged"; // Staged | Confirmed | Rejected
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ConfirmedAt { get; set; }

    public List<ImportStagingRow> Rows { get; private set; } = new();

    private ImportBatch() { }

    public static ImportBatch CreateNew(string sourceFilename, string importType) => new()
    {
        Id = Guid.NewGuid(),
        SourceFilename = sourceFilename,
        ImportType = importType,
        Status = "Staged",
        CreatedAt = DateTimeOffset.UtcNow
    };
}

public sealed class ImportStagingRow
{
    public Guid Id { get; private set; }
    public Guid ImportBatchId { get; private set; }
    public int RowIndex { get; private set; }
    public string RawData { get; private set; } = "{}"; // JSON

    public string? MappedSourceName { get; set; }
    public string? MappedItemName { get; set; }
    public decimal? MappedTotalAmount { get; set; }
    public int? MappedQuantity { get; set; }
    public DateOnly? MappedPurchaseDate { get; set; }

    public string ValidationErrors { get; set; } = "[]"; // JSON array of strings
    public bool IsValid { get; set; }
    public bool PossibleDuplicate { get; set; }

    private ImportStagingRow() { }

    public static ImportStagingRow CreateNew(Guid importBatchId, int rowIndex, string rawDataJson) => new()
    {
        Id = Guid.NewGuid(),
        ImportBatchId = importBatchId,
        RowIndex = rowIndex,
        RawData = rawDataJson
    };
}
