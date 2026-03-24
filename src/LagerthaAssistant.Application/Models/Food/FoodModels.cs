namespace LagerthaAssistant.Application.Models.Food;

public sealed record FoodItemDto(
    int Id,
    string Name,
    string? Category,
    string? Store,
    decimal? Price,
    string? Quantity)
{
    public string? IconEmoji { get; init; }
    public decimal? CurrentQuantity { get; init; }
    public decimal? MinQuantity { get; init; }
}

public sealed record InventoryStatsDto(
    int TotalItems,
    int WithCurrentQuantity,
    int WithMinQuantity,
    int LowStockItems,
    decimal TotalCurrentQuantity);

public sealed record GroceryListItemDto(
    int Id,
    string Name,
    string? Quantity,
    decimal? EstimatedCost,
    string? Store,
    bool IsBought);

public sealed record MealDto(
    int Id,
    string Name,
    int? CaloriesPerServing,
    decimal? ProteinGrams,
    decimal? CarbsGrams,
    decimal? FatGrams,
    int? PrepTimeMinutes,
    int DefaultServings,
    IReadOnlyList<IngredientDto> Ingredients);

public sealed record IngredientDto(
    int FoodItemId,
    string Name,
    string? Quantity,
    string? Store);

public sealed record CalorieSummary(
    DateTime From,
    DateTime To,
    int TotalCalories,
    decimal AvgCaloriesPerDay,
    decimal TotalProteinGrams,
    decimal TotalCarbsGrams,
    decimal TotalFatGrams);

public sealed record MealFrequency(
    int MealId,
    string MealName,
    int TimesEaten,
    DateTime? LastEatenAt);

public sealed record DailyProgressDto(
    int GoalCalories,
    int ConsumedCalories,
    int RemainingCalories,
    decimal PercentComplete,
    int MealsLogged);

public sealed record DietDiversityDto(
    int DaysAnalyzed,
    int UniqueMeals,
    int TotalMeals,
    IReadOnlyList<string> RepeatedMeals,
    IReadOnlyList<string> UniqueMealNames);

public sealed record PortionCalculationDto(
    string MealName,
    int DefaultServings,
    int TargetServings,
    decimal Multiplier,
    IReadOnlyList<ScaledIngredientDto> Ingredients);

public sealed record ScaledIngredientDto(
    string Name,
    string? OriginalQuantity,
    string? ScaledQuantity);

public sealed record FoodSyncSummary(
    int InventoryUpserted,
    int MealsUpserted,
    int GroceryItemsUpserted,
    int IngredientsLinked,
    bool HasErrors,
    string? LastError);

/// <summary>Notion API response envelope for database queries.</summary>
public sealed record NotionQueryResponse(
    IReadOnlyList<NotionPage> Results,
    string? NextCursor,
    bool HasMore);

public sealed record NotionPage(
    string Id,
    string LastEditedTime,
    Dictionary<string, NotionPropertyValue> Properties,
    string? IconEmoji = null);

public sealed record NotionPropertyValue(
    string Type,
    // title / rich_text
    IReadOnlyList<NotionRichTextItem>? Title,
    IReadOnlyList<NotionRichTextItem>? RichText,
    // select
    NotionSelectValue? Select,
    // number
    decimal? Number,
    // checkbox
    bool? Checkbox,
    // date
    NotionDateValue? Date,
    // relation
    IReadOnlyList<NotionRelationItem>? Relation);

public sealed record NotionRichTextItem(string PlainText);

public sealed record NotionSelectValue(string Name);

public sealed record NotionDateValue(string Start);

public sealed record NotionRelationItem(string Id);
