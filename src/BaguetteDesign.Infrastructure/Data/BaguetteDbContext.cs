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
