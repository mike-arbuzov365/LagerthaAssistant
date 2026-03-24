namespace LagerthaAssistant.Application.Interfaces.Food;

using LagerthaAssistant.Application.Models.Food;

public interface INotionFoodClient
{
    Task<IReadOnlyList<NotionPage>> GetInventoryAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotionPage>> GetMealPlansAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotionPage>> GetGroceryListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the Bought? checkbox on a Grocery List page in Notion.
    /// </summary>
    Task MarkGroceryItemBoughtAsync(string notionPageId, bool bought, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates Inventory "Item Quantity" rich-text value in Notion.
    /// </summary>
    Task UpdateInventoryItemQuantityAsync(string notionPageId, string? quantityText, CancellationToken cancellationToken = default);

    /// <summary>
    /// Archives a Notion page (soft delete in Notion UI).
    /// </summary>
    Task ArchivePageAsync(string notionPageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new page in the Notion Grocery List database.
    /// Returns the new page ID.
    /// </summary>
    Task<string> CreateGroceryItemAsync(string name, string? quantity, string? store, CancellationToken cancellationToken = default);
}
