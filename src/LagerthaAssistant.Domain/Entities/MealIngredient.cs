namespace LagerthaAssistant.Domain.Entities;

using LagerthaAssistant.Domain.Common.Base;

/// <summary>
/// Junction table that mirrors the Ingredients Needed relation in Notion Meal Plans ↔ Inventory.
/// </summary>
public sealed class MealIngredient : AuditableEntity
{
    public int MealId { get; set; }

    public int FoodItemId { get; set; }

    /// <summary>Human-readable quantity, e.g. "500g", "2 tbsp".</summary>
    public string? Quantity { get; set; }

    public Meal Meal { get; set; } = null!;

    public FoodItem FoodItem { get; set; } = null!;
}
