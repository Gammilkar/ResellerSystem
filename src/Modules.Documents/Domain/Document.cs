namespace ResellerSystem.Modules.Documents.Domain;

public sealed class Document
{
    public Guid Id { get; private set; }
    public string OriginalFilename { get; private set; } = string.Empty;
    public string StoragePath { get; private set; } = string.Empty;
    public string? MimeType { get; private set; }
    public long SizeBytes { get; private set; }
    public string Sha256Checksum { get; private set; } = string.Empty;
    public DateTimeOffset UploadedAt { get; private set; }

    private Document() { }

    public static Document CreateNew(string originalFilename, string storagePath, string? mimeType, long sizeBytes, string sha256Checksum)
    {
        return new Document
        {
            Id = Guid.NewGuid(),
            OriginalFilename = originalFilename,
            StoragePath = storagePath,
            MimeType = mimeType,
            SizeBytes = sizeBytes,
            Sha256Checksum = sha256Checksum,
            UploadedAt = DateTimeOffset.UtcNow
        };
    }
}

public sealed class DocumentLink
{
    public Guid Id { get; private set; }
    public Guid DocumentId { get; private set; }
    public string EntityType { get; private set; } = string.Empty;
    public Guid EntityId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private DocumentLink() { }

    public static DocumentLink CreateNew(Guid documentId, string entityType, Guid entityId) => new()
    {
        Id = Guid.NewGuid(),
        DocumentId = documentId,
        EntityType = entityType,
        EntityId = entityId,
        CreatedAt = DateTimeOffset.UtcNow
    };
}
