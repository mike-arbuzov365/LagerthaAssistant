namespace BaguetteDesign.Infrastructure.Data;

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
    // (entities will be added in M1 issues)

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder); // applies KernelDbContext configurations
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
