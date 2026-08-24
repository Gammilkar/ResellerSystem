namespace ResellerSystem.Domain.Shared.Dto;

public sealed class DocumentDto
{
    public required Guid Id { get; init; }
    public required string OriginalFilename { get; init; }
    public string? MimeType { get; init; }
    public required long SizeBytes { get; init; }
    public required string Sha256Checksum { get; init; }
    public required DateTimeOffset UploadedAt { get; init; }
    public required IReadOnlyList<DocumentLinkDto> Links { get; init; }
}

public sealed class DocumentLinkDto
{
    public required string EntityType { get; init; }
    public required Guid EntityId { get; init; }
}

public sealed class CreateDocumentLinkRequest
{
    public required string EntityType { get; init; }
    public required Guid EntityId { get; init; }
}
