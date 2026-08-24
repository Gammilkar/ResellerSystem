using Microsoft.EntityFrameworkCore;
using ResellerSystem.Modules.Documents.Domain;
using ResellerSystem.Server.Application.Databases;
using ResellerSystem.Server.Data.Configuration;

namespace ResellerSystem.Modules.Documents.Data;

public sealed class DocumentsDbContext : DbContext
{
    public DocumentsDbContext(DbContextOptions<DocumentsDbContext> options) : base(options) { }

    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentLink> DocumentLinks => Set<DocumentLink>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Document>(entity =>
        {
            entity.ToTable("documents");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.OriginalFilename).HasColumnName("original_filename").IsRequired();
            entity.Property(e => e.StoragePath).HasColumnName("storage_path").IsRequired();
            entity.Property(e => e.MimeType).HasColumnName("mime_type");
            entity.Property(e => e.SizeBytes).HasColumnName("size_bytes");
            entity.Property(e => e.Sha256Checksum).HasColumnName("sha256_checksum");
            entity.Property(e => e.UploadedAt).HasColumnName("uploaded_at");
        });

        modelBuilder.Entity<DocumentLink>(entity =>
        {
            entity.ToTable("document_links");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.DocumentId).HasColumnName("document_id");
            entity.Property(e => e.EntityType).HasColumnName("entity_type").IsRequired();
            entity.Property(e => e.EntityId).HasColumnName("entity_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
        });
    }
}

public interface IDocumentsDbContextFactory
{
    DocumentsDbContext CreateForCurrentTenant();
}

public sealed class DocumentsDbContextFactory : IDocumentsDbContextFactory
{
    private readonly ConnectionStringFactory _connectionStringFactory;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public DocumentsDbContextFactory(ConnectionStringFactory connectionStringFactory, ICurrentTenantAccessor tenantAccessor)
    {
        _connectionStringFactory = connectionStringFactory;
        _tenantAccessor = tenantAccessor;
    }

    public DocumentsDbContext CreateForCurrentTenant()
    {
        var tenant = _tenantAccessor.Require();
        var connectionString = _connectionStringFactory.BuildTenantConnectionString(tenant.PhysicalDatabaseName);
        var options = new DbContextOptionsBuilder<DocumentsDbContext>().UseNpgsql(connectionString).Options;
        return new DocumentsDbContext(options);
    }
}
