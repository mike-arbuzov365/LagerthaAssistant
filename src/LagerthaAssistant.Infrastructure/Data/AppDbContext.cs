namespace LagerthaAssistant.Infrastructure.Data;

using Microsoft.EntityFrameworkCore;
using LagerthaAssistant.Domain.Common.Base;
using LagerthaAssistant.Domain.Entities;
using LagerthaAssistant.Infrastructure.Configurations;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<ConversationSession> ConversationSessions => Set<ConversationSession>();
    public DbSet<ConversationHistoryEntry> ConversationHistoryEntries => Set<ConversationHistoryEntry>();
    public DbSet<UserMemoryEntry> UserMemoryEntries => Set<UserMemoryEntry>();
    public DbSet<SystemPromptEntry> SystemPromptEntries => Set<SystemPromptEntry>();
    public DbSet<VocabularyCard> VocabularyCards => Set<VocabularyCard>();
    public DbSet<VocabularyCardToken> VocabularyCardTokens => Set<VocabularyCardToken>();
    public DbSet<VocabularySyncJob> VocabularySyncJobs => Set<VocabularySyncJob>();
    public DbSet<ConversationIntentMetric> ConversationIntentMetrics => Set<ConversationIntentMetric>();
    public DbSet<TelegramProcessedUpdate> TelegramProcessedUpdates => Set<TelegramProcessedUpdate>();
    public DbSet<GraphAuthToken> GraphAuthTokens => Set<GraphAuthToken>();
    public DbSet<UserAiCredential> UserAiCredentials => Set<UserAiCredential>();

    public DbSet<FoodItem> FoodItems => Set<FoodItem>();
    public DbSet<Meal> Meals => Set<Meal>();
    public DbSet<MealIngredient> MealIngredients => Set<MealIngredient>();
    public DbSet<GroceryListItem> GroceryListItems => Set<GroceryListItem>();
    public DbSet<MealHistory> MealHistory => Set<MealHistory>();
    public DbSet<StoreAlias> StoreAliases => Set<StoreAlias>();
    public DbSet<ItemAlias> ItemAliases => Set<ItemAlias>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new ConversationSessionConfiguration());
        modelBuilder.ApplyConfiguration(new ConversationHistoryEntryConfiguration());
        modelBuilder.ApplyConfiguration(new UserMemoryEntryConfiguration());
        modelBuilder.ApplyConfiguration(new SystemPromptEntryConfiguration());
        modelBuilder.ApplyConfiguration(new VocabularyCardConfiguration());
        modelBuilder.ApplyConfiguration(new VocabularyCardTokenConfiguration());
        modelBuilder.ApplyConfiguration(new VocabularySyncJobConfiguration());
        modelBuilder.ApplyConfiguration(new ConversationIntentMetricConfiguration());
        modelBuilder.ApplyConfiguration(new TelegramProcessedUpdateConfiguration());
        modelBuilder.ApplyConfiguration(new GraphAuthTokenConfiguration());
        modelBuilder.ApplyConfiguration(new UserAiCredentialConfiguration());
        modelBuilder.ApplyConfiguration(new FoodItemConfiguration());
        modelBuilder.ApplyConfiguration(new MealConfiguration());
        modelBuilder.ApplyConfiguration(new MealIngredientConfiguration());
        modelBuilder.ApplyConfiguration(new GroceryListItemConfiguration());
        modelBuilder.ApplyConfiguration(new MealHistoryConfiguration());
        modelBuilder.ApplyConfiguration(new StoreAliasConfiguration());
        modelBuilder.ApplyConfiguration(new ItemAliasConfiguration());
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
