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
    /// Updates Inventory "Item Quantity" (rich-text) and optionally "Min Quantity" (number),
    /// "Price" (number), and "Store" (select) in Notion.
    /// Returns the Notion-authoritative <c>last_edited_time</c> from the PATCH response.
    /// </summary>
    Task<DateTime> UpdateInventoryItemAsync(string notionPageId, string? quantityText, decimal? minQuantity, decimal? price = null, string? store = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Archives a Notion page (soft delete in Notion UI).
    /// </summary>
    Task ArchivePageAsync(string notionPageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new page in the Notion Grocery List database.
    /// Returns the new page ID.
    /// </summary>
    Task<string> CreateGroceryItemAsync(string name, string? quantity, string? store, string? inventoryNotionPageId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new page in the Notion Inventory database.
    /// Returns the new page ID.
    /// </summary>
    Task<string> CreateInventoryItemAsync(
        string name,
        string? store,
        decimal? price,
        string? quantityText,
        string? category = null,
        string? iconEmoji = null,
        CancellationToken cancellationToken = default);
}
