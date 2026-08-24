using Microsoft.EntityFrameworkCore;
using ResellerSystem.Server.Domain.Entities;

namespace ResellerSystem.Server.Data.Master;

/// <summary>
/// EF Core context for the master database — the registry of tenant
/// databases plus server-level Security foundation (users). Schema itself
/// is created/versioned by SqlScriptMigrationRunner (see
/// Migrations/Scripts/Master); this DbContext only maps against the
/// resulting tables for querying/updating, it never generates or applies
/// EF Core migrations.
/// </summary>
public sealed class MasterDbContext : DbContext
{
    public MasterDbContext(DbContextOptions<MasterDbContext> options) : base(options) { }

    public DbSet<DatabaseProfile> DatabaseProfiles => Set<DatabaseProfile>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DatabaseProfile>(entity =>
        {
            entity.ToTable("database_profiles");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.Name).HasColumnName("name").IsRequired();
            entity.Property(e => e.PhysicalDatabaseName).HasColumnName("physical_database_name").IsRequired();
            entity.Property(e => e.TimeZone).HasColumnName("time_zone").IsRequired();
            entity.Property(e => e.Currency).HasColumnName("currency").IsRequired();
            entity.Property(e => e.Status).HasColumnName("status").HasConversion<short>();
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.SchemaVersion).HasColumnName("schema_version");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasIndex(e => e.PhysicalDatabaseName).IsUnique();
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.Username).HasColumnName("username").IsRequired();
            entity.Property(e => e.PasswordHash).HasColumnName("password_hash").IsRequired();
            entity.Property(e => e.PasswordSalt).HasColumnName("password_salt").IsRequired();
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");

            entity.HasIndex(e => e.Username).IsUnique();
        });
    }
}
