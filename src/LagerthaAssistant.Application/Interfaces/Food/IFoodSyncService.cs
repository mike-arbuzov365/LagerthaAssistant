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
    /// Pushes locally-changed GroceryListItems (IsBought flag) back to Notion.
    /// </summary>
    Task<int> SyncGroceryChangesToNotionAsync(int take, CancellationToken cancellationToken = default);
}
