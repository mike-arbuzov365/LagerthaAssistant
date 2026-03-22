namespace LagerthaAssistant.Application.Services.Food;

using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Interfaces.Food;
using LagerthaAssistant.Application.Interfaces.Repositories.Food;
using LagerthaAssistant.Application.Models.Food;
using LagerthaAssistant.Domain.Entities;
using LagerthaAssistant.Domain.Enums;
using Microsoft.Extensions.Logging;

public sealed class FoodTrackingService : IFoodTrackingService
{
    private readonly IFoodItemRepository _foodItemRepo;
    private readonly IMealRepository _mealRepo;
    private readonly IGroceryListRepository _groceryRepo;
    private readonly IMealHistoryRepository _historyRepo;
    private readonly INotionFoodClient _notionClient;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<FoodTrackingService> _logger;

    public FoodTrackingService(
        IFoodItemRepository foodItemRepo,
        IMealRepository mealRepo,
        IGroceryListRepository groceryRepo,
        IMealHistoryRepository historyRepo,
        INotionFoodClient notionClient,
        IUnitOfWork unitOfWork,
        ILogger<FoodTrackingService> logger)
    {
        _foodItemRepo = foodItemRepo;
        _mealRepo = mealRepo;
        _groceryRepo = groceryRepo;
        _historyRepo = historyRepo;
        _notionClient = notionClient;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    // ── Shopping ─────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<GroceryListItemDto>> GetActiveGroceryListAsync(CancellationToken cancellationToken = default)
    {
        var items = await _groceryRepo.GetActiveAsync(cancellationToken);
        return items.Select(MapToDto).ToList();
    }

    public async Task<GroceryListItemDto> AddGroceryItemAsync(
        string name,
        string? quantity,
        string? store,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        _logger.LogInformation("Adding grocery item '{Name}'", name);

        // Create in Notion first so we get a page ID, then persist locally.
        string notionPageId;
        try
        {
            notionPageId = await _notionClient.CreateGroceryItemAsync(name.Trim(), quantity, store, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create grocery item in Notion; will create locally with pending sync");
            notionPageId = $"local:{Guid.NewGuid():N}";
        }

        var item = new GroceryListItem
        {
            NotionPageId = notionPageId,
            Name = name.Trim(),
            Quantity = quantity?.Trim(),
            Store = store?.Trim(),
            IsBought = false,
            NotionUpdatedAt = DateTime.UtcNow,
            NotionSyncStatus = notionPageId.StartsWith("local:", StringComparison.Ordinal)
                ? FoodSyncStatus.Pending
                : FoodSyncStatus.Synced
        };

        await _groceryRepo.AddAsync(item, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(item);
    }

    public async Task<int> MarkItemsBoughtAsync(
        IReadOnlyCollection<int> itemIds,
        CancellationToken cancellationToken = default)
    {
        if (itemIds.Count == 0)
        {
            return 0;
        }

        var all = await _groceryRepo.GetAllAsync(cancellationToken);
        var toMark = all.Where(x => itemIds.Contains(x.Id) && !x.IsBought).ToList();

        foreach (var item in toMark)
        {
            item.IsBought = true;
            item.NotionSyncStatus = FoodSyncStatus.Pending;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return toMark.Count;
    }

    public async Task<int> MarkAllBoughtAsync(CancellationToken cancellationToken = default)
    {
        var marked = await _groceryRepo.MarkAllBoughtAsync(cancellationToken);
        _logger.LogInformation("Marked {Count} grocery items as bought", marked);
        return marked;
    }

    public async Task<int> ClearBoughtItemsAsync(CancellationToken cancellationToken = default)
    {
        var deleted = await _groceryRepo.DeleteBoughtAsync(cancellationToken);
        _logger.LogInformation("Cleared {Count} bought grocery items", deleted);
        return deleted;
    }

    // ── Meals ─────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<MealDto>> GetAllMealsAsync(CancellationToken cancellationToken = default)
    {
        var meals = await _mealRepo.GetAllWithIngredientsAsync(cancellationToken);
        return meals.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<MealDto>> GetCookableNowAsync(CancellationToken cancellationToken = default)
    {
        var allItems = await _foodItemRepo.GetAllAsync(cancellationToken);
        var availableIds = allItems.Select(x => x.Id).ToList();

        var cookable = await _mealRepo.GetCookableFromInventoryAsync(availableIds, cancellationToken);
        return cookable.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<MealFrequency>> GetFavouriteMealsAsync(int take = 5, CancellationToken cancellationToken = default)
    {
        return await _historyRepo.GetTopMealsAsync(take, cancellationToken);
    }

    // ── Meal History & Nutrition ──────────────────────────────────────────────

    public async Task<int> LogMealAsync(
        int mealId,
        decimal servings,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        var meal = await _mealRepo.GetByIdAsync(mealId, cancellationToken)
            ?? throw new InvalidOperationException($"Meal {mealId} not found.");

        var safeServings = Math.Max(0.5m, servings);

        var entry = new MealHistory
        {
            MealId = mealId,
            EatenAt = DateTime.UtcNow,
            Servings = safeServings,
            CaloriesConsumed = meal.CaloriesPerServing.HasValue
                ? (int)Math.Round(meal.CaloriesPerServing.Value * safeServings)
                : null,
            ProteinGrams = meal.ProteinGrams.HasValue
                ? Math.Round(meal.ProteinGrams.Value * safeServings, 1)
                : null,
            CarbsGrams = meal.CarbsGrams.HasValue
                ? Math.Round(meal.CarbsGrams.Value * safeServings, 1)
                : null,
            FatGrams = meal.FatGrams.HasValue
                ? Math.Round(meal.FatGrams.Value * safeServings, 1)
                : null,
            Notes = notes?.Trim()
        };

        await _historyRepo.AddAsync(entry, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Logged meal '{Name}' × {Servings} servings ({Calories} kcal)",
            meal.Name,
            safeServings,
            entry.CaloriesConsumed);

        return entry.Id;
    }

    public async Task<CalorieSummary> GetCalorieSummaryAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        return await _historyRepo.GetCalorieSummaryAsync(from, to, cancellationToken);
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    private static GroceryListItemDto MapToDto(GroceryListItem item) =>
        new(item.Id, item.Name, item.Quantity, item.EstimatedCost, item.Store, item.IsBought);

    private static MealDto MapToDto(Meal meal) =>
        new(
            meal.Id,
            meal.Name,
            meal.CaloriesPerServing,
            meal.ProteinGrams,
            meal.CarbsGrams,
            meal.FatGrams,
            meal.PrepTimeMinutes,
            meal.DefaultServings,
            meal.Ingredients.Select(i => new IngredientDto(
                i.FoodItemId,
                i.FoodItem?.Name ?? string.Empty,
                i.Quantity,
                i.FoodItem?.Store)).ToList());
}
