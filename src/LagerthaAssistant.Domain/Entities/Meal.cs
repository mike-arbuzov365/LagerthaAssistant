namespace LagerthaAssistant.Domain.Entities;

using LagerthaAssistant.Domain.Common.Base;
using LagerthaAssistant.Domain.Enums;

/// <summary>
/// Mirrors the Notion Meal Plans database. Represents a recipe/dish.
/// </summary>
public sealed class Meal : AuditableEntity
{
    public string NotionPageId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Estimated calories per serving. Null until LLM evaluates the recipe.</summary>
    public int? CaloriesPerServing { get; set; }

    /// <summary>Estimated protein per serving in grams.</summary>
    public decimal? ProteinGrams { get; set; }

    /// <summary>Estimated carbohydrates per serving in grams.</summary>
    public decimal? CarbsGrams { get; set; }

    /// <summary>Estimated fat per serving in grams.</summary>
    public decimal? FatGrams { get; set; }

    /// <summary>Estimated preparation time in minutes.</summary>
    public int? PrepTimeMinutes { get; set; }

    public int DefaultServings { get; set; } = 2;

    public FoodSyncStatus NotionSyncStatus { get; set; } = FoodSyncStatus.Synced;

    public int NotionAttemptCount { get; set; }

    public string? NotionLastError { get; set; }

    public DateTime? NotionLastAttemptAt { get; set; }

    public DateTime? NotionSyncedAt { get; set; }

    public DateTime NotionUpdatedAt { get; set; }

    public ICollection<MealIngredient> Ingredients { get; set; } = [];

    public ICollection<MealHistory> History { get; set; } = [];
}
