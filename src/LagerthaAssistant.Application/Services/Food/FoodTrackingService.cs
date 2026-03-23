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

    // ── Inventory ─────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<FoodItemDto>> GetAllInventoryAsync(int take = 50, CancellationToken cancellationToken = default)
    {
        var items = await _foodItemRepo.GetAllAsync(cancellationToken);
        return items.Take(take).Select(MapFoodItemToDto).ToList();
    }

    public async Task<IReadOnlyList<FoodItemDto>> SearchInventoryAsync(string query, int take = 10, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return await GetAllInventoryAsync(take, cancellationToken);

        var items = await _foodItemRepo.SearchByNameAsync(query.Trim(), take, cancellationToken);
        return items.Select(MapFoodItemToDto).ToList();
    }

    public async Task<InventoryStatsDto> GetInventoryStatsAsync(CancellationToken cancellationToken = default)
    {
        var items = await _foodItemRepo.GetAllAsync(cancellationToken);
        if (items.Count == 0)
        {
            return new InventoryStatsDto(0, 0, 0, 0, 0m);
        }

        var withCurrentQuantity = items.Count(x => x.CurrentQuantity.HasValue);
        var withMinQuantity = items.Count(x => x.MinQuantity.HasValue);
        var lowStockItems = items.Count(x =>
            x.CurrentQuantity.HasValue
            && x.MinQuantity.HasValue
            && x.CurrentQuantity.Value < x.MinQuantity.Value);

        var totalCurrentQuantity = items
            .Where(x => x.CurrentQuantity.HasValue)
            .Sum(x => x.CurrentQuantity!.Value);

        return new InventoryStatsDto(
            TotalItems: items.Count,
            WithCurrentQuantity: withCurrentQuantity,
            WithMinQuantity: withMinQuantity,
            LowStockItems: lowStockItems,
            TotalCurrentQuantity: totalCurrentQuantity);
    }

    public async Task<FoodItemDto> AdjustInventoryQuantityAsync(
        int foodItemId,
        decimal delta,
        CancellationToken cancellationToken = default)
    {
        if (delta == 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(delta), "Delta cannot be zero.");
        }

        var item = await _foodItemRepo.GetByIdAsync(foodItemId, cancellationToken)
            ?? throw new InvalidOperationException($"Food item {foodItemId} not found in inventory.");

        var current = item.CurrentQuantity ?? TryParseLeadingNumber(item.Quantity) ?? 0m;
        var updated = Math.Max(0m, current + delta);

        item.CurrentQuantity = Math.Round(updated, 3, MidpointRounding.AwayFromZero);
        item.NotionSyncStatus = FoodSyncStatus.Pending;
        item.NotionUpdatedAt = DateTime.UtcNow;
        item.NotionLastError = null;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Adjusted inventory quantity. FoodItemId={FoodItemId}; Delta={Delta}; CurrentQuantity={CurrentQuantity}",
            item.Id,
            delta,
            item.CurrentQuantity);

        return MapFoodItemToDto(item);
    }

    public async Task<GroceryListItemDto> AddToShoppingFromInventoryAsync(
        int foodItemId,
        string? quantity,
        string? store,
        CancellationToken cancellationToken = default)
    {
        var allItems = await _foodItemRepo.GetAllAsync(cancellationToken);
        var foodItem = allItems.FirstOrDefault(x => x.Id == foodItemId)
            ?? throw new InvalidOperationException($"Food item {foodItemId} not found in inventory.");

        string notionPageId;
        try
        {
            notionPageId = await _notionClient.CreateGroceryItemAsync(foodItem.Name, quantity, store, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create grocery item in Notion for inventory item {Id}; using local id", foodItemId);
            notionPageId = $"local:{Guid.NewGuid():N}";
        }

        var item = new GroceryListItem
        {
            NotionPageId = notionPageId,
            Name = foodItem.Name,
            Quantity = quantity?.Trim(),
            Store = store?.Trim(),
            FoodItemId = foodItemId,
            IsBought = false,
            NotionUpdatedAt = DateTime.UtcNow,
            NotionSyncStatus = notionPageId.StartsWith("local:", StringComparison.Ordinal)
                ? FoodSyncStatus.Pending
                : FoodSyncStatus.Synced
        };

        await _groceryRepo.AddAsync(item, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Added '{Name}' (inventory id={Id}) to shopping list", foodItem.Name, foodItemId);
        return MapToDto(item);
    }

    public async Task<IReadOnlyList<FoodItemDto>> GetLowStockItemsAsync(CancellationToken cancellationToken = default)
    {
        var items = await _foodItemRepo.GetAllAsync(cancellationToken);
        return items
            .Where(x => x.MinQuantity.HasValue && x.CurrentQuantity.HasValue && x.CurrentQuantity.Value < x.MinQuantity.Value)
            .Select(MapFoodItemToDto)
            .ToList();
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
        return await _groceryRepo.MarkBoughtByIdsAsync(itemIds, DateTime.UtcNow, cancellationToken);
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

    public async Task<int> DeleteItemsByIdsAsync(
        IReadOnlyCollection<int> itemIds,
        CancellationToken cancellationToken = default)
    {
        var deleted = await _groceryRepo.DeleteByIdsAsync(itemIds, cancellationToken);
        _logger.LogInformation("Deleted {Count} selected grocery items", deleted);
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

    public async Task<int> LogQuickMealAsync(string name, int calories, decimal servings, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var safeServings = Math.Max(0.5m, servings);

        // Create a temporary meal for this photo log
        var meal = new Meal
        {
            NotionPageId = $"photo:{Guid.NewGuid():N}",
            Name = name.Trim(),
            CaloriesPerServing = calories,
            NotionUpdatedAt = DateTime.UtcNow,
            NotionSyncStatus = FoodSyncStatus.Pending
        };
        await _mealRepo.AddAsync(meal, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var entry = new MealHistory
        {
            MealId = meal.Id,
            EatenAt = DateTime.UtcNow,
            Servings = safeServings,
            CaloriesConsumed = (int)Math.Round(calories * safeServings),
            Notes = "Photo log"
        };

        await _historyRepo.AddAsync(entry, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Photo-logged meal '{Name}' ({Calories} kcal x {Servings})", name, calories, safeServings);
        return entry.Id;
    }

    public async Task<DailyProgressDto> GetDailyProgressAsync(int calorieGoal, CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);
        var summary = await _historyRepo.GetCalorieSummaryAsync(today, tomorrow, cancellationToken);
        var entries = await _historyRepo.GetRangeAsync(today, tomorrow, cancellationToken);
        var consumed = summary.TotalCalories;
        var remaining = Math.Max(0, calorieGoal - consumed);
        var pct = calorieGoal > 0 ? Math.Min(100, (decimal)consumed / calorieGoal * 100) : 0;
        return new DailyProgressDto(calorieGoal, consumed, remaining, Math.Round(pct, 1), entries.Count);
    }

    public async Task<DietDiversityDto> GetDietDiversityAsync(int days = 7, CancellationToken cancellationToken = default)
    {
        var to = DateTime.UtcNow;
        var from = to.AddDays(-days).Date;
        var entries = await _historyRepo.GetRangeAsync(from, to, cancellationToken);

        var mealGroups = entries.GroupBy(e => e.MealId).ToList();
        var uniqueMeals = mealGroups.Count;
        var repeated = mealGroups.Where(g => g.Count() > 1)
            .Select(g => g.First().Meal?.Name ?? $"Meal #{g.Key}")
            .ToList();
        var uniqueNames = mealGroups.Select(g => g.First().Meal?.Name ?? $"Meal #{g.Key}").ToList();

        return new DietDiversityDto(days, uniqueMeals, entries.Count, repeated, uniqueNames);
    }

    public async Task<PortionCalculationDto?> CalculatePortionsAsync(int mealId, int targetServings, CancellationToken cancellationToken = default)
    {
        var meals = await _mealRepo.GetAllWithIngredientsAsync(cancellationToken);
        var mealWithIngr = meals.FirstOrDefault(m => m.Id == mealId);
        if (mealWithIngr is null) return null;

        var defaultServings = Math.Max(1, mealWithIngr.DefaultServings);
        var multiplier = (decimal)targetServings / defaultServings;

        var scaled = mealWithIngr.Ingredients.Select(i =>
        {
            var originalQty = i.Quantity;
            string? scaledQty = null;
            if (!string.IsNullOrWhiteSpace(originalQty))
            {
                scaledQty = ScaleQuantity(originalQty, multiplier);
            }
            return new ScaledIngredientDto(i.FoodItem?.Name ?? "Unknown", originalQty, scaledQty);
        }).ToList();

        return new PortionCalculationDto(mealWithIngr.Name, defaultServings, targetServings, Math.Round(multiplier, 2), scaled);
    }

    private static string ScaleQuantity(string qty, decimal multiplier)
    {
        var match = System.Text.RegularExpressions.Regex.Match(qty.Trim(), @"^(\d+(?:[.,]\d+)?)(.*)$");
        if (match.Success && decimal.TryParse(match.Groups[1].Value.Replace(',', '.'),
                System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var num))
        {
            var scaled = Math.Round(num * multiplier, 1);
            return $"{scaled}{match.Groups[2].Value}";
        }
        return qty;
    }

    private static decimal? TryParseLeadingNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var match = System.Text.RegularExpressions.Regex.Match(value.Trim(), @"^(\d+(?:[.,]\d+)?)");
        if (!match.Success)
        {
            return null;
        }

        return decimal.TryParse(
            match.Groups[1].Value.Replace(',', '.'),
            System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : null;
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

    private static FoodItemDto MapFoodItemToDto(FoodItem item) =>
        new(item.Id, item.Name, item.Category, item.Store, item.Price, item.Quantity)
        {
            CurrentQuantity = item.CurrentQuantity,
            MinQuantity = item.MinQuantity
        };

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
