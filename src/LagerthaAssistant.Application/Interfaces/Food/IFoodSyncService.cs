namespace LagerthaAssistant.Application.Interfaces.Food;

using LagerthaAssistant.Application.Models.Food;

public interface IFoodSyncService
{
    /// <summary>
    /// Pulls all three Notion databases and upserts records in PostgreSQL.
    /// Uses last_edited_time for conflict resolution (Notion wins for structural data).
    /// </summary>
    Task<FoodSyncSummary> SyncFromNotionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Pushes locally-changed GroceryListItems back to Notion.
    /// </summary>
    Task<int> SyncGroceryChangesToNotionAsync(int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pushes locally-changed Inventory quantities back to Notion.
    /// </summary>
    Task<int> SyncInventoryChangesToNotionAsync(int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Archives active Notion Grocery List pages that have no corresponding local record.
    /// Items edited within the grace period are skipped to avoid races with in-flight operations.
    /// Returns the number of pages archived.
    /// </summary>
    Task<int> ReconcileNotionGroceryOrphansAsync(TimeSpan? gracePeriod = null, CancellationToken cancellationToken = default);
}
