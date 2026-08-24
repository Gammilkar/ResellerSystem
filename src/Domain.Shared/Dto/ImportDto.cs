namespace ResellerSystem.Domain.Shared.Dto;

public sealed class ImportStagingRowDto
{
    public required Guid Id { get; init; }
    public required int RowIndex { get; init; }
    public required string RawData { get; init; }
    public string? MappedSourceName { get; init; }
    public string? MappedItemName { get; init; }
    public decimal? MappedTotalAmount { get; init; }
    public int? MappedQuantity { get; init; }
    public required IReadOnlyList<string> ValidationErrors { get; init; }
    public required bool IsValid { get; init; }
    public required bool PossibleDuplicate { get; init; }
}

public sealed class ImportBatchDto
{
    public required Guid Id { get; init; }
    public required string SourceFilename { get; init; }
    public required string Status { get; init; }
    public required int RowCount { get; init; }
    public required int ValidRowCount { get; init; }
    public required IReadOnlyList<ImportStagingRowDto> Rows { get; init; }
}

public sealed class ConfirmImportResultDto
{
    public required int CreatedPurchaseCount { get; init; }
    public required int SkippedRowCount { get; init; }
}
