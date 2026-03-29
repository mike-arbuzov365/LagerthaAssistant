namespace SharedBotKernel.Persistence;

using Microsoft.EntityFrameworkCore;
using SharedBotKernel.Domain.Base;
using SharedBotKernel.Domain.Entities;

public abstract class KernelDbContext : DbContext
{
    protected KernelDbContext(DbContextOptions options) : base(options) { }

    public DbSet<ConversationSession> ConversationSessions => Set<ConversationSession>();
    public DbSet<ConversationHistoryEntry> ConversationHistoryEntries => Set<ConversationHistoryEntry>();
    public DbSet<UserMemoryEntry> UserMemoryEntries => Set<UserMemoryEntry>();
    public DbSet<SystemPromptEntry> SystemPromptEntries => Set<SystemPromptEntry>();
    public DbSet<ConversationIntentMetric> ConversationIntentMetrics => Set<ConversationIntentMetric>();
    public DbSet<TelegramProcessedUpdate> TelegramProcessedUpdates => Set<TelegramProcessedUpdate>();
    public DbSet<GraphAuthToken> GraphAuthTokens => Set<GraphAuthToken>();
    public DbSet<UserAiCredential> UserAiCredentials => Set<UserAiCredential>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(KernelDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var tenantIdProp = entityType.FindProperty(nameof(AuditableEntity.TenantId));
            if (tenantIdProp is not null)
            {
                tenantIdProp.SetMaxLength(64);
            }
        }
    }
}
