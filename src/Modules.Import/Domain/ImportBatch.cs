namespace ResellerSystem.Modules.Import.Domain;

public sealed class ImportBatch
{
    public Guid Id { get; private set; }
    public string SourceFilename { get; private set; } = string.Empty;
    public string ImportType { get; private set; } = "csv-purchases"; // "csv-purchases" | "xlsx-full"
    public string Status { get; set; } = "Staged"; // Staged | Confirmed | Rejected

    /// <summary>JSON object: target field key -> source column name (see
    /// ImportTargetFields). Empty for csv-purchases, which uses a fixed
    /// header shape instead.</summary>
    public string ColumnMapping { get; set; } = "{}";

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ConfirmedAt { get; set; }

    public List<ImportStagingRow> Rows { get; private set; } = new();

    private ImportBatch() { }

    public static ImportBatch CreateNew(string sourceFilename, string importType, string columnMappingJson = "{}") => new()
    {
        Id = Guid.NewGuid(),
        SourceFilename = sourceFilename,
        ImportType = importType,
        ColumnMapping = columnMappingJson,
        Status = "Staged",
        CreatedAt = DateTimeOffset.UtcNow
    };
}

/// <summary>Raw source values only — target-field mapping is applied
/// on-demand (preview) or at Confirm time using the parent batch's
/// ColumnMapping, not duplicated per row.</summary>
public sealed class ImportStagingRow
{
    public Guid Id { get; private set; }
    public Guid ImportBatchId { get; private set; }
    public int RowIndex { get; private set; }
    public string RawData { get; private set; } = "{}"; // JSON object: source column name -> cell value

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

/// <summary>A saved column mapping the user can reuse next time — Product
/// Specification section 58 ("Mapping можно сохранять как шаблон").</summary>
public sealed class ImportMappingTemplate
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string ImportType { get; private set; } = string.Empty;
    public string Mapping { get; set; } = "{}"; // JSON object: target field key -> source column name
    public DateTimeOffset CreatedAt { get; private set; }

    private ImportMappingTemplate() { }

    public static ImportMappingTemplate CreateNew(string name, string importType, string mappingJson) => new()
    {
        Id = Guid.NewGuid(),
        Name = name.Trim(),
        ImportType = importType,
        Mapping = mappingJson,
        CreatedAt = DateTimeOffset.UtcNow
    };
}
