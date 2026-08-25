using Microsoft.EntityFrameworkCore;
using ResellerSystem.Modules.Import.Domain;
using ResellerSystem.Server.Application.Databases;
using ResellerSystem.Server.Data.Configuration;

namespace ResellerSystem.Modules.Import.Data;

public sealed class ImportDbContext : DbContext
{
    public ImportDbContext(DbContextOptions<ImportDbContext> options) : base(options) { }

    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<ImportStagingRow> ImportStagingRows => Set<ImportStagingRow>();
    public DbSet<ImportMappingTemplate> ImportMappingTemplates => Set<ImportMappingTemplate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ImportBatch>(entity =>
        {
            entity.ToTable("import_batches");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.SourceFilename).HasColumnName("source_filename").IsRequired();
            entity.Property(e => e.ImportType).HasColumnName("import_type");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.ColumnMapping).HasColumnName("column_mapping").HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.ConfirmedAt).HasColumnName("confirmed_at");
            entity.HasMany(e => e.Rows).WithOne().HasForeignKey(r => r.ImportBatchId);
        });

        modelBuilder.Entity<ImportStagingRow>(entity =>
        {
            entity.ToTable("import_staging_rows");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.ImportBatchId).HasColumnName("import_batch_id");
            entity.Property(e => e.RowIndex).HasColumnName("row_index");
            entity.Property(e => e.RawData).HasColumnName("raw_data").HasColumnType("jsonb");
            entity.Property(e => e.ValidationErrors).HasColumnName("validation_errors").HasColumnType("jsonb");
            entity.Property(e => e.IsValid).HasColumnName("is_valid");
            entity.Property(e => e.PossibleDuplicate).HasColumnName("possible_duplicate");
        });

        modelBuilder.Entity<ImportMappingTemplate>(entity =>
        {
            entity.ToTable("import_mapping_templates");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.Name).HasColumnName("name").IsRequired();
            entity.Property(e => e.ImportType).HasColumnName("import_type").IsRequired();
            entity.Property(e => e.Mapping).HasColumnName("mapping").HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
        });
    }
}

public interface IImportDbContextFactory
{
    ImportDbContext CreateForCurrentTenant();
}

public sealed class ImportDbContextFactory : IImportDbContextFactory
{
    private readonly ConnectionStringFactory _connectionStringFactory;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public ImportDbContextFactory(ConnectionStringFactory connectionStringFactory, ICurrentTenantAccessor tenantAccessor)
    {
        _connectionStringFactory = connectionStringFactory;
        _tenantAccessor = tenantAccessor;
    }

    public ImportDbContext CreateForCurrentTenant()
    {
        var tenant = _tenantAccessor.Require();
        var connectionString = _connectionStringFactory.BuildTenantConnectionString(tenant.PhysicalDatabaseName);
        var options = new DbContextOptionsBuilder<ImportDbContext>().UseNpgsql(connectionString).Options;
        return new ImportDbContext(options);
    }
}
