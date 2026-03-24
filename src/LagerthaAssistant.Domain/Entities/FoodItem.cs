namespace LagerthaAssistant.Domain.Entities;

using LagerthaAssistant.Domain.Common.Base;
using LagerthaAssistant.Domain.Enums;

/// <summary>
/// Mirrors the Notion Inventory database. Represents an ingredient or product.
/// </summary>
public sealed class FoodItem : AuditableEntity
{
    public string NotionPageId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? IconEmoji { get; set; }

    public string? Category { get; set; }

    public string? Store { get; set; }

    public decimal? Price { get; set; }

    public string? Quantity { get; set; }

    public decimal? CurrentQuantity { get; set; }

    public decimal? MinQuantity { get; set; }

    public DateTime? LastAddedToCartAt { get; set; }

    public FoodSyncStatus NotionSyncStatus { get; set; } = FoodSyncStatus.Synced;

    public int NotionAttemptCount { get; set; }

    public string? NotionLastError { get; set; }

    public DateTime? NotionLastAttemptAt { get; set; }

    public DateTime? NotionSyncedAt { get; set; }

    public DateTime NotionUpdatedAt { get; set; }

    public ICollection<MealIngredient> MealIngredients { get; set; } = [];

    public ICollection<GroceryListItem> GroceryListItems { get; set; } = [];
}
