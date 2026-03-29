namespace BaguetteDesign.Infrastructure.Data;

using BaguetteDesign.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using SharedBotKernel.Persistence;

public sealed class BaguetteDbContext : KernelDbContext
{
    public BaguetteDbContext(DbContextOptions<BaguetteDbContext> options)
        : base(options)
    {
    }

    // ── Kernel DbSets inherited from KernelDbContext ──────────────────────
    // ConversationSessions, ConversationHistoryEntries, UserMemoryEntries,
    // SystemPromptEntries, ConversationIntentMetrics, TelegramProcessedUpdates,
    // GraphAuthTokens, UserAiCredentials

    // ── BaguetteDesign-specific ───────────────────────────────────────────
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<PriceItem> PriceItems => Set<PriceItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder); // applies KernelDbContext configurations

        modelBuilder.Entity<Lead>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.UserId).IsRequired().HasMaxLength(64);
            e.Property(x => x.Status).HasConversion<string>();
            e.HasIndex(x => x.UserId);
        });

        modelBuilder.Entity<PriceItem>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.NotionPageId).IsRequired().HasMaxLength(64);
            e.Property(x => x.Category).IsRequired().HasMaxLength(128);
            e.Property(x => x.Name).IsRequired().HasMaxLength(256);
            e.Property(x => x.Currency).HasMaxLength(8);
            e.Property(x => x.Country).HasMaxLength(64);
            e.HasIndex(x => x.NotionPageId).IsUnique();
            e.HasIndex(x => new { x.Category, x.IsActive });
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SetAuditFields();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        SetAuditFields();
        return base.SaveChanges();
    }

    private void SetAuditFields()
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }
    }
}
