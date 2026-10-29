using FinDoc.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FinDoc.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantMembership> Memberships => Set<TenantMembership>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentChunk> Chunks => Set<DocumentChunk>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Tenant>(e => e.Property(x => x.Name).HasMaxLength(200));

        builder.Entity<TenantMembership>(e =>
        {
            e.Property(x => x.UserId).HasMaxLength(450);
            e.HasIndex(x => x.UserId).IsUnique();
            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId);
        });

        builder.Entity<Document>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.OriginalFileName).HasMaxLength(255);
            e.Property(x => x.Extension).HasMaxLength(10);
            e.Property(x => x.Sha256).HasMaxLength(64);
            e.Property(x => x.StorageKey).HasMaxLength(100);
            e.Property(x => x.FailureCode).HasMaxLength(50);
            e.Property(x => x.FailureMessage).HasMaxLength(200);
            e.Property(x => x.Status).HasConversion<int>();
            e.HasIndex(x => new { x.Status, x.CreatedAt });
            e.HasIndex(x => new { x.TenantId, x.CreatedAt });
            e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId);
        });

        builder.Entity<DocumentChunk>(e =>
        {
            e.Property(x => x.SheetName).HasMaxLength(255);
            e.HasIndex(x => new { x.DocumentId, x.Ordinal }).IsUnique();
            e.HasIndex(x => x.TenantId);
            e.HasOne<Document>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
            // Chunks of soft-deleted documents are invisible to every query.
            e.HasQueryFilter(c => !Documents.Any(d => d.Id == c.DocumentId && d.DeletedAt != null));
        });

        builder.Entity<AuditEvent>(e =>
        {
            e.Property(x => x.Action).HasMaxLength(100);
            e.Property(x => x.TargetUserId).HasMaxLength(450);
            e.Property(x => x.Detail).HasMaxLength(100);
            e.HasIndex(x => new { x.TenantId, x.OccurredAt });
        });
    }
}
