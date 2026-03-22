namespace LagerthaAssistant.Domain.Entities;

using LagerthaAssistant.Domain.Common.Base;

/// <summary>
/// Tracks when a user ate a particular meal. Lives only in PostgreSQL — not synced to Notion.
/// </summary>
public sealed class MealHistory : AuditableEntity
{
    public int MealId { get; set; }

    public DateTime EatenAt { get; set; }

    public decimal Servings { get; set; } = 1;

    /// <summary>
    /// Calories consumed in this sitting (Servings × Meal.CaloriesPerServing at time of logging).
    /// Stored separately so that editing the meal's calorie estimate doesn't skew history.
    /// </summary>
    public int? CaloriesConsumed { get; set; }

    public decimal? ProteinGrams { get; set; }

    public decimal? CarbsGrams { get; set; }

    public decimal? FatGrams { get; set; }

    public string? Notes { get; set; }

    public Meal Meal { get; set; } = null!;
}
