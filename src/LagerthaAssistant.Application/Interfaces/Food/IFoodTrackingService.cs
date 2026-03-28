namespace LagerthaAssistant.Application.Interfaces.Food;

using LagerthaAssistant.Application.Models.Food;

public interface IFoodTrackingService
{
    // ── Inventory ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns inventory items. Pass <c>take &lt;= 0</c> to return all items.
    /// </summary>
    Task<IReadOnlyList<FoodItemDto>> GetAllInventoryAsync(int take = 0, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FoodItemDto>> SearchInventoryAsync(string query, int take = 10, CancellationToken cancellationToken = default);

    Task<InventoryStatsDto> GetInventoryStatsAsync(CancellationToken cancellationToken = default);

    Task<FoodItemDto> AdjustInventoryQuantityAsync(int foodItemId, decimal delta, CancellationToken cancellationToken = default);

    Task<FoodItemDto> SetInventoryCurrentQuantityAsync(int foodItemId, decimal quantity, CancellationToken cancellationToken = default);

    Task<int> ResetAllInventoryCurrentQuantitiesAsync(CancellationToken cancellationToken = default);

    Task<FoodItemDto> SetInventoryMinQuantityAsync(int foodItemId, decimal minQuantity, CancellationToken cancellationToken = default);

    Task<FoodItemDto> UpdateInventoryPriceAndStoreAsync(int foodItemId, decimal? price, string? store, CancellationToken cancellationToken = default);

    /// <summary>Creates a new inventory item locally and in Notion. Returns the created item.</summary>
    Task<FoodItemDto> AddInventoryItemAsync(
        string name,
        string? store,
        decimal? price,
        decimal? currentQuantity,
        string? category = null,
        string? iconEmoji = null,
        CancellationToken cancellationToken = default);

    /// <summary>Returns distinct non-null Store values from inventory.</summary>
    Task<IReadOnlyList<string>> GetDistinctStoresAsync(CancellationToken cancellationToken = default);

    Task<string?> ResolveStoreAliasAsync(string detectedPattern, CancellationToken cancellationToken = default);

    Task SaveStoreAliasAsync(string detectedPattern, string resolvedStoreName, CancellationToken cancellationToken = default);

    Task<int?> ResolveItemAliasAsync(string detectedPattern, CancellationToken cancellationToken = default);

    Task SaveItemAliasAsync(string detectedPattern, int foodItemId, CancellationToken cancellationToken = default);

    Task<GroceryListItemDto> AddToShoppingFromInventoryAsync(int foodItemId, string? quantity, string? store, CancellationToken cancellationToken = default);

    /// <summary>Returns inventory items where CurrentQuantity is below MinQuantity.</summary>
    Task<IReadOnlyList<FoodItemDto>> GetLowStockItemsAsync(CancellationToken cancellationToken = default);

    // ── Shopping ────────────────────────────────────────────────────────────

    Task<IReadOnlyList<GroceryListItemDto>> GetActiveGroceryListAsync(CancellationToken cancellationToken = default);

    Task<GroceryListItemDto> AddGroceryItemAsync(string name, string? quantity, string? store, CancellationToken cancellationToken = default);

    Task<int> MarkItemsBoughtAsync(IReadOnlyCollection<int> itemIds, CancellationToken cancellationToken = default);

    Task<int> MarkAllBoughtAsync(CancellationToken cancellationToken = default);

    Task<int> ClearBoughtItemsAsync(CancellationToken cancellationToken = default);

    Task<int> DeleteItemsByIdsAsync(IReadOnlyCollection<int> itemIds, CancellationToken cancellationToken = default);

    // ── Meals ────────────────────────────────────────────────────────────────

    Task<IReadOnlyList<MealDto>> GetAllMealsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MealDto>> GetCookableNowAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MealFrequency>> GetFavouriteMealsAsync(int take = 5, CancellationToken cancellationToken = default);

    Task<MealDto> CreateMealAsync(
        string name,
        int? caloriesPerServing,
        decimal? proteinGrams,
        decimal? carbsGrams,
        decimal? fatGrams,
        int? prepTimeMinutes,
        int defaultServings,
        IReadOnlyList<(string Name, string? Quantity)> ingredients,
        CancellationToken cancellationToken = default);

    // ── Meal History & Nutrition ─────────────────────────────────────────────

    /// <summary>Logs a meal and returns the ID of the created history entry.</summary>
    Task<int> LogMealAsync(int mealId, decimal servings, string? notes, CancellationToken cancellationToken = default);

    Task<CalorieSummary> GetCalorieSummaryAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);

    /// <summary>Logs a quick meal from a photo (creates a temporary meal record).</summary>
    Task<int> LogQuickMealAsync(string name, int calories, decimal servings, CancellationToken cancellationToken = default);

    /// <summary>Returns daily calorie progress against a goal.</summary>
    Task<DailyProgressDto> GetDailyProgressAsync(int calorieGoal, CancellationToken cancellationToken = default);

    /// <summary>Analyses diet diversity over the given number of days.</summary>
    Task<DietDiversityDto> GetDietDiversityAsync(int days = 7, CancellationToken cancellationToken = default);

    /// <summary>Calculates scaled ingredient quantities for a given number of servings.</summary>
    Task<PortionCalculationDto?> CalculatePortionsAsync(int mealId, int targetServings, CancellationToken cancellationToken = default);
}
