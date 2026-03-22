namespace LagerthaAssistant.Application.Interfaces.Food;

using LagerthaAssistant.Application.Models.Food;

public interface IFoodTrackingService
{
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
}
