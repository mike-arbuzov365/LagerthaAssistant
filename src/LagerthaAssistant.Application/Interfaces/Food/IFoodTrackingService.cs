namespace LagerthaAssistant.Application.Interfaces.Food;

using LagerthaAssistant.Application.Models.Food;

public interface IFoodTrackingService
{
    // ── Inventory ────────────────────────────────────────────────────────────

    Task<IReadOnlyList<FoodItemDto>> GetAllInventoryAsync(int take = 50, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FoodItemDto>> SearchInventoryAsync(string query, int take = 10, CancellationToken cancellationToken = default);

    Task<GroceryListItemDto> AddToShoppingFromInventoryAsync(int foodItemId, string? quantity, string? store, CancellationToken cancellationToken = default);

    /// <summary>Returns inventory items where CurrentQuantity is below MinQuantity.</summary>
    Task<IReadOnlyList<FoodItemDto>> GetLowStockItemsAsync(CancellationToken cancellationToken = default);

    // ── Shopping ────────────────────────────────────────────────────────────

    Task<IReadOnlyList<GroceryListItemDto>> GetActiveGroceryListAsync(CancellationToken cancellationToken = default);

    Task<GroceryListItemDto> AddGroceryItemAsync(string name, string? quantity, string? store, CancellationToken cancellationToken = default);

    Task<int> MarkItemsBoughtAsync(IReadOnlyCollection<int> itemIds, CancellationToken cancellationToken = default);

    Task<int> MarkAllBoughtAsync(CancellationToken cancellationToken = default);

    Task<int> ClearBoughtItemsAsync(CancellationToken cancellationToken = default);

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
