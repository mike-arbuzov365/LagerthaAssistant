namespace LagerthaAssistant.Infrastructure.Data;

using Microsoft.EntityFrameworkCore;
using SharedBotKernel.Domain.Base;
using LagerthaAssistant.Domain.Entities;
using LagerthaAssistant.Infrastructure.Configurations;
using SharedBotKernel.Persistence;

public sealed class AppDbContext : KernelDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    // ── Kernel DbSets inherited from KernelDbContext ──────────────────────
    // ConversationSessions, ConversationHistoryEntries, UserMemoryEntries,
    // SystemPromptEntries, ConversationIntentMetrics, TelegramProcessedUpdates,
    // GraphAuthTokens, UserAiCredentials

    // ── Lagertha-specific ─────────────────────────────────────────────────
    public DbSet<VocabularyCard> VocabularyCards => Set<VocabularyCard>();
    public DbSet<VocabularyCardToken> VocabularyCardTokens => Set<VocabularyCardToken>();
    public DbSet<VocabularySyncJob> VocabularySyncJobs => Set<VocabularySyncJob>();

    public DbSet<FoodItem> FoodItems => Set<FoodItem>();
    public DbSet<Meal> Meals => Set<Meal>();
    public DbSet<MealIngredient> MealIngredients => Set<MealIngredient>();
    public DbSet<GroceryListItem> GroceryListItems => Set<GroceryListItem>();
    public DbSet<MealHistory> MealHistory => Set<MealHistory>();
    public DbSet<StoreAlias> StoreAliases => Set<StoreAlias>();
    public DbSet<ItemAlias> ItemAliases => Set<ItemAlias>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder); // applies KernelDbContext configurations

        modelBuilder.ApplyConfiguration(new VocabularyCardConfiguration());
        modelBuilder.ApplyConfiguration(new VocabularyCardTokenConfiguration());
        modelBuilder.ApplyConfiguration(new VocabularySyncJobConfiguration());
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
