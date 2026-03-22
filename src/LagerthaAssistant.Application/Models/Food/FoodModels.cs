namespace LagerthaAssistant.Application.Models.Food;

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
    Dictionary<string, NotionPropertyValue> Properties);

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
