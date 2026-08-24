using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using ResellerSystem.Domain.Shared.Dto;
using ResellerSystem.Modules.Documents.Data;
using ResellerSystem.Modules.Documents.Domain;
using ResellerSystem.Server.Application.Databases;
using ResellerSystem.Server.Application.Exceptions;
using ResellerSystem.Server.FileStorage;

namespace ResellerSystem.Modules.Documents.Application;

public interface IDocumentsService
{
    Task<DocumentDto> UploadAsync(Stream content, string originalFilename, string? mimeType, CancellationToken ct = default);
    Task<DocumentDto> LinkAsync(Guid documentId, CreateDocumentLinkRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentDto>> ListForEntityAsync(string entityType, Guid entityId, CancellationToken ct = default);
    Task<(Stream Content, string MimeType, string Filename)> DownloadAsync(Guid documentId, CancellationToken ct = default);
}

public sealed class DocumentsService : IDocumentsService
{
    private readonly IDocumentsDbContextFactory _dbContextFactory;
    private readonly IFileStorageService _fileStorage;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public DocumentsService(IDocumentsDbContextFactory dbContextFactory, IFileStorageService fileStorage, ICurrentTenantAccessor tenantAccessor)
    {
        _dbContextFactory = dbContextFactory;
        _fileStorage = fileStorage;
        _tenantAccessor = tenantAccessor;
    }

    public async Task<DocumentDto> UploadAsync(Stream content, string originalFilename, string? mimeType, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(originalFilename))
            throw new ValidationFailedException(new[] { "Filename is required." });

        var tenant = _tenantAccessor.Require();

        // Content-addressable storage under {StorageRoot}/{tenantDbName}/{hash[0:2]}/{hash[2:4]}/{hash}.{ext} —
        // Architecture Plan v0.1 section 9: documents stored in original
        // form, never compressed/re-encoded. Hashing requires buffering to
        // compute the name before we know it, so we spool to a temp file
        // first rather than holding the whole upload in memory.
        var tempPath = Path.Combine(_fileStorage.TempRoot, $"{Guid.NewGuid():N}.upload");
        Directory.CreateDirectory(_fileStorage.TempRoot);

        string checksum;
        long sizeBytes;
        await using (var tempFile = File.Create(tempPath))
        {
            await content.CopyToAsync(tempFile, ct);
            sizeBytes = tempFile.Length;
        }

        await using (var readStream = File.OpenRead(tempPath))
        {
            checksum = Convert.ToHexString(await SHA256.HashDataAsync(readStream, ct)).ToLowerInvariant();
        }

        var extension = Path.GetExtension(originalFilename);
        var relativePath = Path.Combine(tenant.PhysicalDatabaseName, checksum[..2], checksum[2..4], checksum + extension);
        var finalPath = Path.Combine(_fileStorage.StorageRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(finalPath)!);

        if (!File.Exists(finalPath))
        {
            File.Move(tempPath, finalPath);
        }
        else
        {
            File.Delete(tempPath); // identical content already stored — natural de-duplication
        }

        await using var db = _dbContextFactory.CreateForCurrentTenant();
        var document = Document.CreateNew(originalFilename, relativePath, mimeType, sizeBytes, checksum);
        db.Documents.Add(document);
        await db.SaveChangesAsync(ct);

        return ToDto(document, Array.Empty<DocumentLink>());
    }

    public async Task<DocumentDto> LinkAsync(Guid documentId, CreateDocumentLinkRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.EntityType))
            throw new ValidationFailedException(new[] { "EntityType is required." });

        await using var db = _dbContextFactory.CreateForCurrentTenant();

        var document = await db.Documents.FirstOrDefaultAsync(d => d.Id == documentId, ct)
            ?? throw new NotFoundException("DOCUMENT_NOT_FOUND", "Document was not found.");

        var link = DocumentLink.CreateNew(documentId, request.EntityType, request.EntityId);
        db.DocumentLinks.Add(link);
        await db.SaveChangesAsync(ct);

        var links = await db.DocumentLinks.Where(l => l.DocumentId == documentId).ToListAsync(ct);
        return ToDto(document, links);
    }

    public async Task<IReadOnlyList<DocumentDto>> ListForEntityAsync(string entityType, Guid entityId, CancellationToken ct = default)
    {
        await using var db = _dbContextFactory.CreateForCurrentTenant();

        var documentIds = await db.DocumentLinks
            .Where(l => l.EntityType == entityType && l.EntityId == entityId)
            .Select(l => l.DocumentId)
            .ToListAsync(ct);

        var documents = await db.Documents.Where(d => documentIds.Contains(d.Id)).ToListAsync(ct);
        var allLinks = await db.DocumentLinks.Where(l => documentIds.Contains(l.DocumentId)).ToListAsync(ct);

        return documents.Select(d => ToDto(d, allLinks.Where(l => l.DocumentId == d.Id))).ToList();
    }

    public async Task<(Stream Content, string MimeType, string Filename)> DownloadAsync(Guid documentId, CancellationToken ct = default)
    {
        await using var db = _dbContextFactory.CreateForCurrentTenant();

        var document = await db.Documents.FirstOrDefaultAsync(d => d.Id == documentId, ct)
            ?? throw new NotFoundException("DOCUMENT_NOT_FOUND", "Document was not found.");

        var fullPath = Path.Combine(_fileStorage.StorageRoot, document.StoragePath);
        if (!File.Exists(fullPath))
        {
            throw new NotFoundException("DOCUMENT_FILE_MISSING", "Document metadata exists but the file is missing from storage.");
        }

        return (File.OpenRead(fullPath), document.MimeType ?? "application/octet-stream", document.OriginalFilename);
    }

    private static DocumentDto ToDto(Document d, IEnumerable<DocumentLink> links) => new()
    {
        Id = d.Id,
        OriginalFilename = d.OriginalFilename,
        MimeType = d.MimeType,
        SizeBytes = d.SizeBytes,
        Sha256Checksum = d.Sha256Checksum,
        UploadedAt = d.UploadedAt,
        Links = links.Select(l => new DocumentLinkDto { EntityType = l.EntityType, EntityId = l.EntityId }).ToList()
    };
}
