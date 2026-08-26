using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using ResellerSystem.Modules.Documents.Application;
using ResellerSystem.Modules.Documents.Data;
using ResellerSystem.Modules.Documents.Domain;
using ResellerSystem.Server.Application.Exceptions;
using ResellerSystem.Server.FileStorage;
using Xunit;

namespace ResellerSystem.Modules.Documents.Tests;

/// <summary>
/// Regression coverage for a real gap found during a Purchases-module audit:
/// no delete/unlink existed anywhere in the Documents module. This needs a
/// real DbContext + real filesystem round-trip (not the DB-free validation
/// tier used elsewhere in this codebase, e.g. PurchaseServiceValidationTests)
/// since the behavior under test IS the persistence/file-cleanup logic —
/// same reasoning as PurchaseServiceSupplierPersistenceTests.cs.
/// </summary>
public class DocumentsServiceDeleteLinkTests : IDisposable
{
    private readonly IDocumentsDbContextFactory _dbContextFactory = Substitute.For<IDocumentsDbContextFactory>();
    private readonly IFileStorageService _fileStorage = Substitute.For<IFileStorageService>();
    private readonly DocumentsService _sut;
    private readonly string _dbName = Guid.NewGuid().ToString();
    private readonly string _storageRoot;

    public DocumentsServiceDeleteLinkTests()
    {
        _storageRoot = Path.Combine(Path.GetTempPath(), "DocumentsServiceDeleteLinkTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_storageRoot);
        _fileStorage.StorageRoot.Returns(_storageRoot);

        _dbContextFactory.CreateForCurrentTenant().Returns(_ => NewContext());
        _sut = new DocumentsService(_dbContextFactory, _fileStorage, Substitute.For<Server.Application.Databases.ICurrentTenantAccessor>());
    }

    public void Dispose()
    {
        if (Directory.Exists(_storageRoot)) Directory.Delete(_storageRoot, recursive: true);
    }

    private DocumentsDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<DocumentsDbContext>().UseInMemoryDatabase(_dbName).Options;
        return new DocumentsDbContext(options);
    }

    private string WriteStoredFile(string relativePath)
    {
        var fullPath = Path.Combine(_storageRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, "content");
        return fullPath;
    }

    [Fact]
    public async Task DeleteLinkAsync_removes_only_the_targeted_link_when_others_remain()
    {
        var document = Document.CreateNew("receipt.pdf", "ab/cd/abcd.pdf", "application/pdf", 100, "abcd");
        var purchaseId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var purchaseLink = DocumentLink.CreateNew(document.Id, "Purchase", purchaseId);
        var itemLink = DocumentLink.CreateNew(document.Id, "Item", itemId);

        await using (var db = NewContext())
        {
            db.Documents.Add(document);
            db.DocumentLinks.AddRange(purchaseLink, itemLink);
            await db.SaveChangesAsync();
        }

        await _sut.DeleteLinkAsync(document.Id, "Purchase", purchaseId);

        await using var verifyDb = NewContext();
        (await verifyDb.Documents.AnyAsync(d => d.Id == document.Id)).Should().BeTrue();
        (await verifyDb.DocumentLinks.AnyAsync(l => l.EntityType == "Purchase" && l.EntityId == purchaseId)).Should().BeFalse();
        (await verifyDb.DocumentLinks.AnyAsync(l => l.EntityType == "Item" && l.EntityId == itemId)).Should().BeTrue();
    }

    [Fact]
    public async Task DeleteLinkAsync_deletes_the_document_row_when_it_was_the_last_link()
    {
        var document = Document.CreateNew("receipt.pdf", "ab/cd/lastlink.pdf", "application/pdf", 100, "lastlink");
        var purchaseId = Guid.NewGuid();
        WriteStoredFile(document.StoragePath);

        await using (var db = NewContext())
        {
            db.Documents.Add(document);
            db.DocumentLinks.Add(DocumentLink.CreateNew(document.Id, "Purchase", purchaseId));
            await db.SaveChangesAsync();
        }

        await _sut.DeleteLinkAsync(document.Id, "Purchase", purchaseId);

        await using var verifyDb = NewContext();
        (await verifyDb.Documents.AnyAsync(d => d.Id == document.Id)).Should().BeFalse();
        File.Exists(Path.Combine(_storageRoot, document.StoragePath)).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteLinkAsync_keeps_the_physical_file_when_another_document_row_shares_the_storage_path()
    {
        var sharedPath = "ab/cd/shared.pdf";
        var document1 = Document.CreateNew("receipt-copy-1.pdf", sharedPath, "application/pdf", 100, "shared");
        var document2 = Document.CreateNew("receipt-copy-2.pdf", sharedPath, "application/pdf", 100, "shared");
        var purchaseId = Guid.NewGuid();
        WriteStoredFile(sharedPath);

        await using (var db = NewContext())
        {
            db.Documents.AddRange(document1, document2);
            db.DocumentLinks.Add(DocumentLink.CreateNew(document1.Id, "Purchase", purchaseId));
            await db.SaveChangesAsync();
        }

        await _sut.DeleteLinkAsync(document1.Id, "Purchase", purchaseId);

        await using var verifyDb = NewContext();
        (await verifyDb.Documents.AnyAsync(d => d.Id == document1.Id)).Should().BeFalse();
        (await verifyDb.Documents.AnyAsync(d => d.Id == document2.Id)).Should().BeTrue();
        File.Exists(Path.Combine(_storageRoot, sharedPath)).Should().BeTrue();
    }

    [Fact]
    public async Task DeleteLinkAsync_throws_NotFound_for_an_unknown_link()
    {
        var act = async () => await _sut.DeleteLinkAsync(Guid.NewGuid(), "Purchase", Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
