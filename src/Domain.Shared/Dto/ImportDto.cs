namespace ResellerSystem.Domain.Shared.Dto;

public sealed class ImportTargetFieldDto
{
    public required string Key { get; init; }
    public required string DisplayName { get; init; }
    public required string Group { get; init; }
    public bool Required { get; init; }
}

public sealed class InspectXlsxResultDto
{
    public required IReadOnlyList<string> Columns { get; init; }
}

public sealed class ImportMappingTemplateDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string ImportType { get; init; }
    public required IReadOnlyDictionary<string, string> Mapping { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}

public sealed class SaveMappingTemplateRequest
{
    public required string Name { get; init; }
    public required string ImportType { get; init; }
    public required IReadOnlyDictionary<string, string> Mapping { get; init; }
}

public sealed class ImportStagingRowDto
{
    public required Guid Id { get; init; }
    public required int RowIndex { get; init; }
    public required IReadOnlyDictionary<string, string> RawData { get; init; }

    /// <summary>RawData resolved through the batch's column mapping, for
    /// preview — target field key -> value.</summary>
    public required IReadOnlyDictionary<string, string> MappedPreview { get; init; }

    public required IReadOnlyList<string> ValidationErrors { get; init; }
    public required bool IsValid { get; init; }
    public required bool PossibleDuplicate { get; init; }
}

public sealed class ImportBatchDto
{
    public required Guid Id { get; init; }
    public required string SourceFilename { get; init; }
    public required string ImportType { get; init; }
    public required string Status { get; init; }
    public required int RowCount { get; init; }
    public required int ValidRowCount { get; init; }
    public required IReadOnlyList<ImportStagingRowDto> Rows { get; init; }
}

public sealed class ConfirmImportResultDto
{
    public required int CreatedPurchaseCount { get; init; }
    public required int CreatedItemCount { get; init; }
    public required int CreatedListingCount { get; init; }
    public required int CreatedSaleCount { get; init; }
    public required int CreatedReturnCount { get; init; }
    public required int SkippedRowCount { get; init; }
}
