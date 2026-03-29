namespace LagerthaAssistant.Domain.Entities;

using LagerthaAssistant.Domain.Enums;

/// <summary>
/// Mirrors the Notion Grocery List database. Represents an item on the active shopping list.
/// </summary>
public sealed class GroceryListItem : AuditableEntity
{
    public string NotionPageId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Quantity { get; set; }

    public decimal? EstimatedCost { get; set; }

    public string? Store { get; set; }

    public bool IsBought { get; set; }

    /// <summary>Optional link back to the source FoodItem in Inventory.</summary>
    public int? FoodItemId { get; set; }

    /// <summary>
    /// When set, this item is a tombstone — logically deleted but retained to prevent zombie re-creation
    /// during the next Notion pull-sync. Tombstones are periodically purged after a retention period.
    /// </summary>
    public DateTime? ArchivedAt { get; set; }

    public FoodSyncStatus NotionSyncStatus { get; set; } = FoodSyncStatus.Synced;

    public int NotionAttemptCount { get; set; }

    public string? NotionLastError { get; set; }

    public DateTime? NotionLastAttemptAt { get; set; }

    public DateTime? NotionSyncedAt { get; set; }

    public DateTime NotionUpdatedAt { get; set; }

    public FoodItem? FoodItem { get; set; }
}
