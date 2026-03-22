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
        var availableIds = await _foodItemRepo.GetAllIdsAsync(cancellationToken);

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

    public async Task<MealDto> CreateMealAsync(
        string name,
        int? caloriesPerServing,
        decimal? proteinGrams,
        decimal? carbsGrams,
        decimal? fatGrams,
        int? prepTimeMinutes,
        int defaultServings,
        IReadOnlyList<(string Name, string? Quantity)> ingredients,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var meal = new Meal
        {
            NotionPageId = $"local:{Guid.NewGuid():N}",
            Name = name.Trim(),
            CaloriesPerServing = caloriesPerServing,
            ProteinGrams = proteinGrams,
            CarbsGrams = carbsGrams,
            FatGrams = fatGrams,
            PrepTimeMinutes = prepTimeMinutes,
            DefaultServings = defaultServings > 0 ? defaultServings : 2,
            NotionUpdatedAt = DateTime.UtcNow,
            NotionSyncStatus = FoodSyncStatus.Pending
        };

        await _mealRepo.AddAsync(meal, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Link ingredients: try to find existing FoodItems by name, create new ones if not found
        foreach (var (ingredientName, qty) in ingredients)
        {
            var found = await _foodItemRepo.SearchByNameAsync(ingredientName.Trim(), take: 1, cancellationToken);
            int foodItemId;
            if (found.Count > 0)
            {
                foodItemId = found[0].Id;
            }
            else
            {
                var newItem = new FoodItem
                {
                    NotionPageId = $"local:{Guid.NewGuid():N}",
                    Name = ingredientName.Trim(),
                    NotionUpdatedAt = DateTime.UtcNow,
                    NotionSyncStatus = FoodSyncStatus.Pending
                };
                await _foodItemRepo.AddAsync(newItem, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                foodItemId = newItem.Id;
            }

            meal.Ingredients.Add(new MealIngredient
            {
                MealId = meal.Id,
                FoodItemId = foodItemId,
                Quantity = qty?.Trim()
            });
        }

        if (meal.Ingredients.Count > 0)
            await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created meal '{Name}' with {Count} ingredients", meal.Name, meal.Ingredients.Count);
        return MapToDto(meal);
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
