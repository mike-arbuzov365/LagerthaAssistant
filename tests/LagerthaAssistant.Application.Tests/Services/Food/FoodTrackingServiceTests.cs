namespace LagerthaAssistant.Application.Tests.Services.Food;

using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Interfaces.Food;
using LagerthaAssistant.Application.Interfaces.Repositories.Food;
using LagerthaAssistant.Application.Models.Food;
using LagerthaAssistant.Application.Services.Food;
using LagerthaAssistant.Domain.Entities;
using LagerthaAssistant.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public sealed class FoodTrackingServiceTests
{
    // ── GetActiveGroceryListAsync ────────────────────────────────────────────

    [Fact]
    public async Task GetActiveGroceryListAsync_ShouldReturnMappedDtos()
    {
        var repo = new FakeGroceryListRepository
        {
            ActiveItems =
            [
                new GroceryListItem { Id = 1, Name = "Milk", Quantity = "2L", Store = "Costco", IsBought = false },
                new GroceryListItem { Id = 2, Name = "Bread", IsBought = false }
            ]
        };
        var sut = CreateSut(groceryRepo: repo);

        var result = await sut.GetActiveGroceryListAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("Milk", result[0].Name);
        Assert.Equal("2L", result[0].Quantity);
        Assert.Equal("Costco", result[0].Store);
        Assert.Equal("Bread", result[1].Name);
    }

    [Fact]
    public async Task GetActiveGroceryListAsync_ShouldReturnEmpty_WhenNoItems()
    {
        var sut = CreateSut();
        var result = await sut.GetActiveGroceryListAsync();
        Assert.Empty(result);
    }

    // ── AddGroceryItemAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task AddGroceryItemAsync_ShouldCreateItemWithNotionId_WhenNotionSucceeds()
    {
        var notion = new FakeNotionFoodClient { CreatedPageId = "notion-page-123" };
        var repo = new FakeGroceryListRepository();
        var uow = new FakeUnitOfWork();
        var sut = CreateSut(groceryRepo: repo, notionClient: notion, unitOfWork: uow);

        var result = await sut.AddGroceryItemAsync("Eggs", "12", "Walmart");

        Assert.Equal("Eggs", result.Name);
        Assert.Equal("12", result.Quantity);
        Assert.Equal("Walmart", result.Store);
        Assert.Single(repo.AddedItems);
        Assert.Equal("notion-page-123", repo.AddedItems[0].NotionPageId);
        Assert.Equal(FoodSyncStatus.Synced, repo.AddedItems[0].NotionSyncStatus);
        Assert.Equal(1, uow.SaveCount);
    }

    [Fact]
    public async Task AddGroceryItemAsync_ShouldFallbackToLocalId_WhenNotionFails()
    {
        var notion = new FakeNotionFoodClient { ShouldThrow = true };
        var repo = new FakeGroceryListRepository();
        var sut = CreateSut(groceryRepo: repo, notionClient: notion);

        var result = await sut.AddGroceryItemAsync("Butter", null, null);

        Assert.Equal("Butter", result.Name);
        Assert.Single(repo.AddedItems);
        Assert.StartsWith("local:", repo.AddedItems[0].NotionPageId, StringComparison.Ordinal);
        Assert.Equal(FoodSyncStatus.Pending, repo.AddedItems[0].NotionSyncStatus);
    }

    [Fact]
    public async Task AddGroceryItemAsync_ShouldThrow_WhenNameIsEmpty()
    {
        var sut = CreateSut();
        await Assert.ThrowsAsync<ArgumentException>(() => sut.AddGroceryItemAsync("  ", null, null));
    }

    [Fact]
    public async Task AddGroceryItemAsync_ShouldTrimName()
    {
        var repo = new FakeGroceryListRepository();
        var sut = CreateSut(groceryRepo: repo);

        await sut.AddGroceryItemAsync("  Cheese  ", null, null);

        Assert.Equal("Cheese", repo.AddedItems[0].Name);
    }

    // ── MarkItemsBoughtAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task MarkItemsBoughtAsync_ShouldReturnCount_WhenItemsFound()
    {
        var repo = new FakeGroceryListRepository
        {
            AllItems =
            [
                new GroceryListItem { Id = 1, IsBought = false },
                new GroceryListItem { Id = 2, IsBought = false },
                new GroceryListItem { Id = 3, IsBought = false }
            ]
        };
        var uow = new FakeUnitOfWork();
        var sut = CreateSut(groceryRepo: repo, unitOfWork: uow);

        var count = await sut.MarkItemsBoughtAsync([1, 2]);

        Assert.Equal(2, count);
        Assert.True(repo.AllItems[0].IsBought);
        Assert.True(repo.AllItems[1].IsBought);
        Assert.False(repo.AllItems[2].IsBought);
        Assert.Equal(FoodSyncStatus.Pending, repo.AllItems[0].NotionSyncStatus);
        Assert.Equal(0, uow.SaveCount);
        Assert.Equal([1, 2], repo.LastMarkedItemIds);
        Assert.NotNull(repo.LastMarkedUpdatedAtUtc);
    }

    [Fact]
    public async Task MarkItemsBoughtAsync_ShouldSkipAlreadyBought()
    {
        var repo = new FakeGroceryListRepository
        {
            AllItems = [new GroceryListItem { Id = 1, IsBought = true }]
        };
        var sut = CreateSut(groceryRepo: repo);

        var count = await sut.MarkItemsBoughtAsync([1]);

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task MarkItemsBoughtAsync_ShouldReturnZero_WhenEmptyList()
    {
        var uow = new FakeUnitOfWork();
        var sut = CreateSut(unitOfWork: uow);

        var count = await sut.MarkItemsBoughtAsync([]);

        Assert.Equal(0, count);
        Assert.Equal(0, uow.SaveCount);
    }

    // ── MarkAllBoughtAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task MarkAllBoughtAsync_ShouldReturnRepoCount()
    {
        var repo = new FakeGroceryListRepository { MarkAllBoughtResult = 5 };
        var sut = CreateSut(groceryRepo: repo);

        var count = await sut.MarkAllBoughtAsync();

        Assert.Equal(5, count);
    }

    // ── ClearBoughtItemsAsync ────────────────────────────────────────────────

    [Fact]
    public async Task ClearBoughtItemsAsync_ShouldReturnRepoCount()
    {
        var repo = new FakeGroceryListRepository { DeleteBoughtResult = 3 };
        var sut = CreateSut(groceryRepo: repo);

        var count = await sut.ClearBoughtItemsAsync();

        Assert.Equal(3, count);
    }

    [Fact]
    public async Task DeleteItemsByIdsAsync_ShouldMarkSelectedItemsAsBought()
    {
        var repo = new FakeGroceryListRepository
        {
            AllItems =
            [
                new GroceryListItem { Id = 1, Name = "Milk", IsBought = false },
                new GroceryListItem { Id = 2, Name = "Bread", IsBought = false }
            ]
        };
        var sut = CreateSut(groceryRepo: repo);

        var count = await sut.DeleteItemsByIdsAsync([1]);

        Assert.Equal(1, count);
        Assert.Equal([1], repo.LastMarkedItemIds);
        Assert.NotNull(repo.LastMarkedUpdatedAtUtc);
    }

    // ── GetAllMealsAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllMealsAsync_ShouldReturnMappedDtos_WithIngredients()
    {
        var foodItem = new FoodItem { Id = 10, Name = "Pasta", Store = "Costco" };
        var meal = new Meal
        {
            Id = 1,
            Name = "Pasta Carbonara",
            CaloriesPerServing = 600,
            ProteinGrams = 25m,
            CarbsGrams = 70m,
            FatGrams = 20m,
            PrepTimeMinutes = 30,
            DefaultServings = 2,
            Ingredients =
            [
                new MealIngredient { FoodItemId = 10, FoodItem = foodItem, Quantity = "200g" }
            ]
        };
        var repo = new FakeMealRepository { AllMeals = [meal] };
        var sut = CreateSut(mealRepo: repo);

        var result = await sut.GetAllMealsAsync();

        Assert.Single(result);
        var dto = result[0];
        Assert.Equal("Pasta Carbonara", dto.Name);
        Assert.Equal(600, dto.CaloriesPerServing);
        Assert.Equal(25m, dto.ProteinGrams);
        Assert.Single(dto.Ingredients);
        Assert.Equal("Pasta", dto.Ingredients[0].Name);
        Assert.Equal("200g", dto.Ingredients[0].Quantity);
    }

    [Fact]
    public async Task GetAllMealsAsync_ShouldReturnEmpty_WhenNoMeals()
    {
        var sut = CreateSut();
        var result = await sut.GetAllMealsAsync();
        Assert.Empty(result);
    }

    // ── GetCookableNowAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetCookableNowAsync_ShouldPassAvailableIdsToRepo()
    {
        var foodItems = new List<FoodItem>
        {
            new() { Id = 1 }, new() { Id = 2 }, new() { Id = 5 }
        };
        var foodItemRepo = new FakeFoodItemRepository { AllItems = foodItems };
        var mealRepo = new FakeMealRepository
        {
            CookableFactory = ids =>
            {
                Assert.Equal(3, ids.Count);
                Assert.Contains(1, ids);
                Assert.Contains(2, ids);
                Assert.Contains(5, ids);
                return [];
            }
        };
        var sut = CreateSut(foodItemRepo: foodItemRepo, mealRepo: mealRepo);

        await sut.GetCookableNowAsync();

        Assert.True(mealRepo.CookableWasCalled);
    }

    [Fact]
    public async Task GetCookableNowAsync_ShouldReturnMappedDtos()
    {
        var foodItemRepo = new FakeFoodItemRepository { AllItems = [new FoodItem { Id = 1 }] };
        var mealRepo = new FakeMealRepository
        {
            CookableFactory = _ => [new Meal { Id = 1, Name = "Soup", DefaultServings = 4, Ingredients = [] }]
        };
        var sut = CreateSut(foodItemRepo: foodItemRepo, mealRepo: mealRepo);

        var result = await sut.GetCookableNowAsync();

        Assert.Single(result);
        Assert.Equal("Soup", result[0].Name);
    }

    // ── GetFavouriteMealsAsync ───────────────────────────────────────────────

    [Fact]
    public async Task GetFavouriteMealsAsync_ShouldDelegateToHistoryRepo()
    {
        var history = new FakeMealHistoryRepository
        {
            TopMeals = [new MealFrequency(1, "Pizza", 10, DateTime.UtcNow)]
        };
        var sut = CreateSut(historyRepo: history);

        var result = await sut.GetFavouriteMealsAsync(take: 1);

        Assert.Single(result);
        Assert.Equal("Pizza", result[0].MealName);
        Assert.Equal(10, result[0].TimesEaten);
    }

    // ── LogMealAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task LogMealAsync_ShouldCreateHistoryEntry_WithCalculatedNutrition()
    {
        var meal = new Meal
        {
            Id = 1,
            Name = "Steak",
            CaloriesPerServing = 400,
            ProteinGrams = 50m,
            CarbsGrams = 5m,
            FatGrams = 20m
        };
        var mealRepo = new FakeMealRepository { MealById = meal };
        var historyRepo = new FakeMealHistoryRepository();
        var uow = new FakeUnitOfWork();
        var sut = CreateSut(mealRepo: mealRepo, historyRepo: historyRepo, unitOfWork: uow);

        await sut.LogMealAsync(1, servings: 2m, notes: "Lunch");

        Assert.Single(historyRepo.AddedEntries);
        var entry = historyRepo.AddedEntries[0];
        Assert.Equal(1, entry.MealId);
        Assert.Equal(2m, entry.Servings);
        Assert.Equal(800, entry.CaloriesConsumed);       // 400 × 2
        Assert.Equal(100m, entry.ProteinGrams);          // 50 × 2
        Assert.Equal(10m, entry.CarbsGrams);             // 5 × 2
        Assert.Equal(40m, entry.FatGrams);               // 20 × 2
        Assert.Equal("Lunch", entry.Notes);
        Assert.Equal(1, uow.SaveCount);
    }

    [Fact]
    public async Task LogMealAsync_ShouldClampServingsTo05_WhenZeroOrNegative()
    {
        var meal = new Meal { Id = 1, Name = "Rice", CaloriesPerServing = 200 };
        var mealRepo = new FakeMealRepository { MealById = meal };
        var historyRepo = new FakeMealHistoryRepository();
        var sut = CreateSut(mealRepo: mealRepo, historyRepo: historyRepo);

        await sut.LogMealAsync(1, servings: 0m, notes: null);

        Assert.Equal(0.5m, historyRepo.AddedEntries[0].Servings);
        Assert.Equal(100, historyRepo.AddedEntries[0].CaloriesConsumed); // 200 × 0.5
    }

    [Fact]
    public async Task LogMealAsync_ShouldLeaveNutritionNull_WhenMealHasNoCalorieData()
    {
        var meal = new Meal { Id = 1, Name = "Mystery dish" }; // no nutrition values
        var mealRepo = new FakeMealRepository { MealById = meal };
        var historyRepo = new FakeMealHistoryRepository();
        var sut = CreateSut(mealRepo: mealRepo, historyRepo: historyRepo);

        await sut.LogMealAsync(1, 1m, null);

        var entry = historyRepo.AddedEntries[0];
        Assert.Null(entry.CaloriesConsumed);
        Assert.Null(entry.ProteinGrams);
        Assert.Null(entry.CarbsGrams);
        Assert.Null(entry.FatGrams);
    }

    [Fact]
    public async Task LogMealAsync_ShouldThrow_WhenMealNotFound()
    {
        var sut = CreateSut(); // mealRepo returns null by default
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.LogMealAsync(999, 1m, null));
    }

    [Fact]
    public async Task LogMealAsync_ShouldRoundCalories_ToNearestInteger()
    {
        var meal = new Meal { Id = 1, Name = "Snack", CaloriesPerServing = 100 };
        var mealRepo = new FakeMealRepository { MealById = meal };
        var historyRepo = new FakeMealHistoryRepository();
        var sut = CreateSut(mealRepo: mealRepo, historyRepo: historyRepo);

        await sut.LogMealAsync(1, servings: 1.5m, notes: null);

        Assert.Equal(150, historyRepo.AddedEntries[0].CaloriesConsumed); // 100 × 1.5 = 150
    }

    [Fact]
    public async Task LogMealAsync_ShouldTrimNotes()
    {
        var meal = new Meal { Id = 1, Name = "Salad" };
        var mealRepo = new FakeMealRepository { MealById = meal };
        var historyRepo = new FakeMealHistoryRepository();
        var sut = CreateSut(mealRepo: mealRepo, historyRepo: historyRepo);

        await sut.LogMealAsync(1, 1m, "  notes with spaces  ");

        Assert.Equal("notes with spaces", historyRepo.AddedEntries[0].Notes);
    }

    // ── GetCalorieSummaryAsync ───────────────────────────────────────────────

    [Fact]
    public async Task GetCalorieSummaryAsync_ShouldDelegateToHistoryRepo()
    {
        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 1, 7, 0, 0, 0, DateTimeKind.Utc);
        var expected = new CalorieSummary(from, to, 14000, 2000m, 700m, 1800m, 560m);
        var historyRepo = new FakeMealHistoryRepository { Summary = expected };
        var sut = CreateSut(historyRepo: historyRepo);

        var result = await sut.GetCalorieSummaryAsync(from, to);

        Assert.Equal(expected, result);
        Assert.Equal(from, historyRepo.LastFrom);
        Assert.Equal(to, historyRepo.LastTo);
    }

    // ── LogQuickMealAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task LogQuickMealAsync_ShouldCreateMealAndHistoryEntry()
    {
        var mealRepo = new FakeMealRepository();
        var historyRepo = new FakeMealHistoryRepository();
        var uow = new FakeUnitOfWork();
        var sut = CreateSut(mealRepo: mealRepo, historyRepo: historyRepo, unitOfWork: uow);

        await sut.LogQuickMealAsync("Sandwich", 350, 1m);

        Assert.Single(mealRepo.AllMeals);
        Assert.Equal("Sandwich", mealRepo.AllMeals[0].Name);
        Assert.Equal(350, mealRepo.AllMeals[0].CaloriesPerServing);
        Assert.StartsWith("photo:", mealRepo.AllMeals[0].NotionPageId, StringComparison.Ordinal);

        Assert.Single(historyRepo.AddedEntries);
        Assert.Equal(350, historyRepo.AddedEntries[0].CaloriesConsumed);
        Assert.Equal(1m, historyRepo.AddedEntries[0].Servings);
        Assert.Equal("Photo log", historyRepo.AddedEntries[0].Notes);
    }

    [Fact]
    public async Task LogQuickMealAsync_ShouldClampServingsTo05_WhenZeroOrNegative()
    {
        var mealRepo = new FakeMealRepository();
        var historyRepo = new FakeMealHistoryRepository();
        var sut = CreateSut(mealRepo: mealRepo, historyRepo: historyRepo);

        await sut.LogQuickMealAsync("Snack", 200, 0m);

        Assert.Equal(0.5m, historyRepo.AddedEntries[0].Servings);
        Assert.Equal(100, historyRepo.AddedEntries[0].CaloriesConsumed);
    }

    [Fact]
    public async Task LogQuickMealAsync_ShouldThrow_WhenNameIsEmpty()
    {
        var sut = CreateSut();
        await Assert.ThrowsAsync<ArgumentException>(() => sut.LogQuickMealAsync("  ", 300, 1m));
    }

    [Fact]
    public async Task LogQuickMealAsync_ShouldScaleCalories_ByServings()
    {
        var mealRepo = new FakeMealRepository();
        var historyRepo = new FakeMealHistoryRepository();
        var sut = CreateSut(mealRepo: mealRepo, historyRepo: historyRepo);

        await sut.LogQuickMealAsync("Pasta", 400, 2.5m);

        Assert.Equal(1000, historyRepo.AddedEntries[0].CaloriesConsumed); // 400 × 2.5
    }

    // ── GetDailyProgressAsync ────────────────────────────────────────────────

    [Fact]
    public async Task GetDailyProgressAsync_ShouldComputeProgressFromSummary()
    {
        var summary = new CalorieSummary(DateTime.MinValue, DateTime.MaxValue, 1500, 214, 100, 180, 50);
        var historyRepo = new FakeMealHistoryRepository
        {
            Summary = summary,
            RangeEntries = [new MealHistory(), new MealHistory(), new MealHistory()]
        };
        var sut = CreateSut(historyRepo: historyRepo);

        var result = await sut.GetDailyProgressAsync(calorieGoal: 2000);

        Assert.Equal(2000, result.GoalCalories);
        Assert.Equal(1500, result.ConsumedCalories);
        Assert.Equal(500, result.RemainingCalories);
        Assert.Equal(75m, result.PercentComplete);
        Assert.Equal(3, result.MealsLogged);
    }

    [Fact]
    public async Task GetDailyProgressAsync_ShouldReturnZeroProgress_WhenNoHistory()
    {
        var sut = CreateSut();

        var result = await sut.GetDailyProgressAsync(calorieGoal: 2000);

        Assert.Equal(2000, result.GoalCalories);
        Assert.Equal(0, result.ConsumedCalories);
        Assert.Equal(2000, result.RemainingCalories);
        Assert.Equal(0m, result.PercentComplete);
        Assert.Equal(0, result.MealsLogged);
    }

    [Fact]
    public async Task GetDailyProgressAsync_ShouldCapPercentAt100_WhenGoalExceeded()
    {
        var summary = new CalorieSummary(DateTime.MinValue, DateTime.MaxValue, 3000, 428, 200, 300, 100);
        var historyRepo = new FakeMealHistoryRepository { Summary = summary };
        var sut = CreateSut(historyRepo: historyRepo);

        var result = await sut.GetDailyProgressAsync(calorieGoal: 2000);

        Assert.Equal(100m, result.PercentComplete);
        Assert.Equal(0, result.RemainingCalories); // clamped to 0
    }

    // ── GetDietDiversityAsync ────────────────────────────────────────────────

    [Fact]
    public async Task GetDietDiversityAsync_ShouldReturnEmpty_WhenNoHistory()
    {
        var sut = CreateSut();

        var result = await sut.GetDietDiversityAsync(7);

        Assert.Equal(0, result.UniqueMeals);
        Assert.Equal(0, result.TotalMeals);
        Assert.Empty(result.RepeatedMeals);
    }

    [Fact]
    public async Task GetDietDiversityAsync_ShouldDetectRepeatedMeals()
    {
        var meal1 = new Meal { Id = 1, Name = "Pasta", NotionPageId = "n1" };
        var meal2 = new Meal { Id = 2, Name = "Soup", NotionPageId = "n2" };
        var historyRepo = new FakeMealHistoryRepository
        {
            RangeEntries =
            [
                new MealHistory { MealId = 1, Meal = meal1 },
                new MealHistory { MealId = 1, Meal = meal1 }, // repeated
                new MealHistory { MealId = 2, Meal = meal2 }
            ]
        };
        var sut = CreateSut(historyRepo: historyRepo);

        var result = await sut.GetDietDiversityAsync(7);

        Assert.Equal(2, result.UniqueMeals);
        Assert.Equal(3, result.TotalMeals);
        Assert.Contains("Pasta", result.RepeatedMeals);
        Assert.DoesNotContain("Soup", result.RepeatedMeals);
    }

    [Fact]
    public async Task GetDietDiversityAsync_ShouldListAllUniqueNames()
    {
        var meal1 = new Meal { Id = 1, Name = "Eggs", NotionPageId = "n1" };
        var meal2 = new Meal { Id = 2, Name = "Rice", NotionPageId = "n2" };
        var historyRepo = new FakeMealHistoryRepository
        {
            RangeEntries =
            [
                new MealHistory { MealId = 1, Meal = meal1 },
                new MealHistory { MealId = 2, Meal = meal2 }
            ]
        };
        var sut = CreateSut(historyRepo: historyRepo);

        var result = await sut.GetDietDiversityAsync(7);

        Assert.Contains("Eggs", result.UniqueMealNames);
        Assert.Contains("Rice", result.UniqueMealNames);
    }

    // ── CalculatePortionsAsync ───────────────────────────────────────────────

    [Fact]
    public async Task CalculatePortionsAsync_ShouldReturnNull_WhenMealNotFound()
    {
        var sut = CreateSut();
        var result = await sut.CalculatePortionsAsync(999, 4);
        Assert.Null(result);
    }

    [Fact]
    public async Task CalculatePortionsAsync_ShouldScaleIngredients_ByTargetServings()
    {
        var foodItem = new FoodItem { Id = 1, Name = "Flour" };
        var meal = new Meal
        {
            Id = 1,
            Name = "Cake",
            NotionPageId = "n1",
            DefaultServings = 2,
            Ingredients =
            [
                new MealIngredient { FoodItemId = 1, FoodItem = foodItem, Quantity = "200g" }
            ]
        };
        var mealRepo = new FakeMealRepository { AllMeals = [meal] };
        var sut = CreateSut(mealRepo: mealRepo);

        var result = await sut.CalculatePortionsAsync(1, targetServings: 4);

        Assert.NotNull(result);
        Assert.Equal("Cake", result.MealName);
        Assert.Equal(2, result.DefaultServings);
        Assert.Equal(4, result.TargetServings);
        Assert.Equal(2m, result.Multiplier);
        Assert.Single(result.Ingredients);
        Assert.Equal("Flour", result.Ingredients[0].Name);
        Assert.Equal("200g", result.Ingredients[0].OriginalQuantity);
        Assert.Equal("400g", result.Ingredients[0].ScaledQuantity);
    }

    [Fact]
    public async Task CalculatePortionsAsync_ShouldLeaveScaledQuantityNull_WhenQuantityIsNull()
    {
        var meal = new Meal
        {
            Id = 1,
            Name = "Dish",
            NotionPageId = "n1",
            DefaultServings = 1,
            Ingredients = [new MealIngredient { FoodItemId = 1, Quantity = null }]
        };
        var mealRepo = new FakeMealRepository { AllMeals = [meal] };
        var sut = CreateSut(mealRepo: mealRepo);

        var result = await sut.CalculatePortionsAsync(1, targetServings: 2);

        Assert.Null(result!.Ingredients[0].ScaledQuantity);
    }

    // ── CreateMealAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task CreateMealAsync_ShouldCreateMealWithNoIngredients()
    {
        var mealRepo = new FakeMealRepository();
        var uow = new FakeUnitOfWork();
        var sut = CreateSut(mealRepo: mealRepo, unitOfWork: uow);

        var result = await sut.CreateMealAsync("Salad", 300, 20m, 15m, 10m, 10, 2, []);

        Assert.Equal("Salad", result.Name);
        Assert.Equal(300, result.CaloriesPerServing);
        Assert.Single(mealRepo.AllMeals);
        Assert.StartsWith("local:", mealRepo.AllMeals[0].NotionPageId, StringComparison.Ordinal);
        Assert.Empty(result.Ingredients);
    }

    [Fact]
    public async Task CreateMealAsync_ShouldLinkExistingFoodItem_WhenFound()
    {
        var existingFood = new FoodItem { Id = 5, Name = "Tomato" };
        var foodRepo = new FakeFoodItemRepository { AllItems = [existingFood] };
        var mealRepo = new FakeMealRepository();
        var sut = CreateSut(foodItemRepo: foodRepo, mealRepo: mealRepo);

        await sut.CreateMealAsync("Salad", null, null, null, null, null, 2,
            [("Tomato", "2 pcs")]);

        Assert.Single(mealRepo.AllMeals[0].Ingredients);
        Assert.Equal(5, mealRepo.AllMeals[0].Ingredients.First().FoodItemId);
        Assert.Equal("2 pcs", mealRepo.AllMeals[0].Ingredients.First().Quantity);
    }

    [Fact]
    public async Task CreateMealAsync_ShouldCreateNewFoodItem_WhenIngredientNotFound()
    {
        var foodRepo = new FakeFoodItemRepository();
        var mealRepo = new FakeMealRepository();
        var uow = new FakeUnitOfWork();
        var sut = CreateSut(foodItemRepo: foodRepo, mealRepo: mealRepo, unitOfWork: uow);

        await sut.CreateMealAsync("Pasta", null, null, null, null, null, 2,
            [("Garlic", "3 cloves")]);

        Assert.Single(foodRepo.AllItems);
        Assert.Equal("Garlic", foodRepo.AllItems[0].Name);
        Assert.Equal(foodRepo.AllItems[0].Id, mealRepo.AllMeals[0].Ingredients.First().FoodItemId);
    }

    [Fact]
    public async Task CreateMealAsync_ShouldThrow_WhenNameIsEmpty()
    {
        var sut = CreateSut();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.CreateMealAsync("  ", null, null, null, null, null, 2, []));
    }

    [Fact]
    public async Task CreateMealAsync_ShouldDefaultServingsTo2_WhenZeroPassed()
    {
        var mealRepo = new FakeMealRepository();
        var sut = CreateSut(mealRepo: mealRepo);

        await sut.CreateMealAsync("Rice", null, null, null, null, null, defaultServings: 0, []);

        Assert.Equal(2, mealRepo.AllMeals[0].DefaultServings);
    }

    // ── GetAllInventoryAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GetAllInventoryAsync_ShouldReturnMappedDtos()
    {
        var repo = new FakeFoodItemRepository
        {
            AllItems =
            [
                new FoodItem { Id = 1, Name = "Milk", Category = "Dairy", Quantity = "2L" },
                new FoodItem { Id = 2, Name = "Eggs" }
            ]
        };
        var sut = CreateSut(foodItemRepo: repo);

        var result = await sut.GetAllInventoryAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal(1, result[0].Id);
        Assert.Equal("Milk", result[0].Name);
        Assert.Equal("Dairy", result[0].Category);
        Assert.Equal("2L", result[0].Quantity);
        Assert.Equal("Eggs", result[1].Name);
    }

    [Fact]
    public async Task GetAllInventoryAsync_ShouldApplyTakeLimit()
    {
        var repo = new FakeFoodItemRepository
        {
            AllItems = Enumerable.Range(1, 10)
                .Select(i => new FoodItem { Id = i, Name = $"Item{i}" })
                .ToList()
        };
        var sut = CreateSut(foodItemRepo: repo);

        var result = await sut.GetAllInventoryAsync(take: 3);

        Assert.Equal(3, result.Count);
    }

    // ── SearchInventoryAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task SearchInventoryAsync_ShouldReturnMatchingItems()
    {
        var repo = new FakeFoodItemRepository
        {
            AllItems =
            [
                new FoodItem { Id = 1, Name = "Whole Milk" },
                new FoodItem { Id = 2, Name = "Oat Milk" },
                new FoodItem { Id = 3, Name = "Eggs" }
            ]
        };
        var sut = CreateSut(foodItemRepo: repo);

        var result = await sut.SearchInventoryAsync("milk");

        Assert.Equal(2, result.Count);
        Assert.All(result, x => Assert.Contains("Milk", x.Name, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SearchInventoryAsync_ShouldReturnAll_WhenQueryIsEmpty()
    {
        var repo = new FakeFoodItemRepository
        {
            AllItems =
            [
                new FoodItem { Id = 1, Name = "Milk" },
                new FoodItem { Id = 2, Name = "Eggs" }
            ]
        };
        var sut = CreateSut(foodItemRepo: repo);

        var result = await sut.SearchInventoryAsync("  ");

        Assert.Equal(2, result.Count);
    }

    // ── AddToShoppingFromInventoryAsync ──────────────────────────────────────

    [Fact]
    public async Task AddToShoppingFromInventoryAsync_ShouldLinkFoodItemId()
    {
        var foodRepo = new FakeFoodItemRepository
        {
            AllItems = [new FoodItem { Id = 7, Name = "Butter", Store = "Costco" }]
        };
        var groceryRepo = new FakeGroceryListRepository();
        var uow = new FakeUnitOfWork();
        var sut = CreateSut(foodItemRepo: foodRepo, groceryRepo: groceryRepo, unitOfWork: uow);

        var result = await sut.AddToShoppingFromInventoryAsync(7, "200g", store: null);

        Assert.Equal("Butter", result.Name);
        Assert.Equal("200g", result.Quantity);
        Assert.Single(groceryRepo.AddedItems);
        Assert.Equal(7, groceryRepo.AddedItems[0].FoodItemId);
        Assert.Equal(1, uow.SaveCount);
    }

    [Fact]
    public async Task AddToShoppingFromInventoryAsync_ShouldThrow_WhenFoodItemNotFound()
    {
        var sut = CreateSut();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.AddToShoppingFromInventoryAsync(999, null, null));
    }

    // ── GetLowStockItemsAsync ────────────────────────────────────────────────

    [Fact]
    public async Task GetLowStockItemsAsync_ShouldReturnItemsBelowMinQuantity()
    {
        var repo = new FakeFoodItemRepository
        {
            AllItems =
            [
                new FoodItem { Id = 1, Name = "Eggs", CurrentQuantity = 2m, MinQuantity = 6m },
                new FoodItem { Id = 2, Name = "Milk", CurrentQuantity = 1m, MinQuantity = 2m },
                new FoodItem { Id = 3, Name = "Flour", CurrentQuantity = 5m, MinQuantity = 3m } // above min — NOT low
            ]
        };
        var sut = CreateSut(foodItemRepo: repo);

        var result = await sut.GetLowStockItemsAsync();

        Assert.Equal(2, result.Count);
        Assert.All(result, x => Assert.True(x.CurrentQuantity < x.MinQuantity));
    }

    [Fact]
    public async Task GetLowStockItemsAsync_ShouldExcludeItems_WithNoMinQuantity()
    {
        var repo = new FakeFoodItemRepository
        {
            AllItems =
            [
                new FoodItem { Id = 1, Name = "Salt", CurrentQuantity = 0m, MinQuantity = null },
                new FoodItem { Id = 2, Name = "Eggs", CurrentQuantity = null, MinQuantity = 6m }
            ]
        };
        var sut = CreateSut(foodItemRepo: repo);

        var result = await sut.GetLowStockItemsAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetLowStockItemsAsync_ShouldMapCurrentAndMinQuantity()
    {
        var repo = new FakeFoodItemRepository
        {
            AllItems =
            [
                new FoodItem { Id = 1, Name = "Butter", CurrentQuantity = 0.5m, MinQuantity = 1m }
            ]
        };
        var sut = CreateSut(foodItemRepo: repo);

        var result = await sut.GetLowStockItemsAsync();

        Assert.Single(result);
        Assert.Equal(0.5m, result[0].CurrentQuantity);
        Assert.Equal(1m, result[0].MinQuantity);
    }

    // —— Inventory stats + quantity adjustments —————————————————————————————————————————————————————————————————

    [Fact]
    public async Task GetInventoryStatsAsync_ShouldReturnAggregatedStats()
    {
        var repo = new FakeFoodItemRepository
        {
            AllItems =
            [
                new FoodItem { Id = 1, Name = "Milk", CurrentQuantity = 2m, MinQuantity = 3m },
                new FoodItem { Id = 2, Name = "Eggs", CurrentQuantity = 10m, MinQuantity = 6m },
                new FoodItem { Id = 3, Name = "Salt", CurrentQuantity = null, MinQuantity = null }
            ]
        };
        var sut = CreateSut(foodItemRepo: repo);

        var stats = await sut.GetInventoryStatsAsync();

        Assert.Equal(3, stats.TotalItems);
        Assert.Equal(2, stats.WithCurrentQuantity);
        Assert.Equal(2, stats.WithMinQuantity);
        Assert.Equal(1, stats.LowStockItems);
        Assert.Equal(12m, stats.TotalCurrentQuantity);
    }

    [Fact]
    public async Task AdjustInventoryQuantityAsync_ShouldUpdateCurrentQuantity_FromParsedTextWhenCurrentNull()
    {
        var item = new FoodItem { Id = 10, Name = "Milk", Quantity = "2.5L", CurrentQuantity = null };
        var repo = new FakeFoodItemRepository { AllItems = [item] };
        var uow = new FakeUnitOfWork();
        var sut = CreateSut(foodItemRepo: repo, unitOfWork: uow);

        var updated = await sut.AdjustInventoryQuantityAsync(10, -1m);

        Assert.Equal(1.5m, updated.CurrentQuantity);
        Assert.Equal(FoodSyncStatus.Pending, item.NotionSyncStatus);
        Assert.Equal(1, uow.SaveCount);
    }

    [Fact]
    public async Task AdjustInventoryQuantityAsync_ShouldClampToZero_WhenDeltaIsNegative()
    {
        var item = new FoodItem { Id = 11, Name = "Butter", CurrentQuantity = 0.5m };
        var repo = new FakeFoodItemRepository { AllItems = [item] };
        var sut = CreateSut(foodItemRepo: repo);

        var updated = await sut.AdjustInventoryQuantityAsync(11, -2m);

        Assert.Equal(0m, updated.CurrentQuantity);
    }

    [Fact]
    public async Task AdjustInventoryQuantityAsync_ShouldThrow_WhenDeltaIsZero()
    {
        var item = new FoodItem { Id = 12, Name = "Flour", CurrentQuantity = 1m };
        var repo = new FakeFoodItemRepository { AllItems = [item] };
        var sut = CreateSut(foodItemRepo: repo);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => sut.AdjustInventoryQuantityAsync(12, 0m));
    }

    [Fact]
    public async Task SetInventoryCurrentQuantityAsync_ShouldSetExactQuantity_AndMarkPendingSync()
    {
        var item = new FoodItem { Id = 21, Name = "Apple", CurrentQuantity = 1m, NotionSyncStatus = FoodSyncStatus.Synced };
        var repo = new FakeFoodItemRepository { AllItems = [item] };
        var uow = new FakeUnitOfWork();
        var sut = CreateSut(foodItemRepo: repo, unitOfWork: uow);

        var updated = await sut.SetInventoryCurrentQuantityAsync(21, 4.25m);

        Assert.Equal(4.25m, updated.CurrentQuantity);
        Assert.Equal(4.25m, item.CurrentQuantity);
        Assert.Equal(FoodSyncStatus.Pending, item.NotionSyncStatus);
        Assert.Equal(1, uow.SaveCount);
    }

    [Fact]
    public async Task ResetAllInventoryCurrentQuantitiesAsync_ShouldSetNonZeroAndNullToZero()
    {
        var repo = new FakeFoodItemRepository
        {
            AllItems =
            [
                new FoodItem { Id = 1, Name = "Beer", CurrentQuantity = 3m, NotionSyncStatus = FoodSyncStatus.Synced },
                new FoodItem { Id = 2, Name = "Juice", CurrentQuantity = 0m, NotionSyncStatus = FoodSyncStatus.Synced },
                new FoodItem { Id = 3, Name = "Wine", CurrentQuantity = null, NotionSyncStatus = FoodSyncStatus.Synced }
            ]
        };
        var uow = new FakeUnitOfWork();
        var sut = CreateSut(foodItemRepo: repo, unitOfWork: uow);

        var updatedCount = await sut.ResetAllInventoryCurrentQuantitiesAsync();

        Assert.Equal(2, updatedCount);
        Assert.All(repo.AllItems.Where(item => item.Id != 2), item => Assert.Equal(0m, item.CurrentQuantity));
        Assert.Equal(FoodSyncStatus.Pending, repo.AllItems[0].NotionSyncStatus);
        Assert.Equal(FoodSyncStatus.Pending, repo.AllItems[2].NotionSyncStatus);
        Assert.Equal(FoodSyncStatus.Synced, repo.AllItems[1].NotionSyncStatus);
        Assert.Equal(1, uow.SaveCount);
    }

    [Fact]
    public async Task SetInventoryMinQuantityAsync_ShouldUpdateMinQuantity_AndMarkPendingSync()
    {
        var item = new FoodItem { Id = 30, Name = "Apple", MinQuantity = 1m, NotionSyncStatus = FoodSyncStatus.Synced };
        var repo = new FakeFoodItemRepository { AllItems = [item] };
        var uow = new FakeUnitOfWork();
        var sut = CreateSut(foodItemRepo: repo, unitOfWork: uow);

        var updated = await sut.SetInventoryMinQuantityAsync(30, 3m);

        Assert.Equal(3m, updated.MinQuantity);
        Assert.Equal(3m, item.MinQuantity);
        Assert.Equal(FoodSyncStatus.Pending, item.NotionSyncStatus);
        Assert.Equal(1, uow.SaveCount);
    }

    [Fact]
    public async Task SetInventoryMinQuantityAsync_ShouldThrow_WhenMinQuantityIsNegative()
    {
        var item = new FoodItem { Id = 31, Name = "Milk", MinQuantity = 1m };
        var repo = new FakeFoodItemRepository { AllItems = [item] };
        var sut = CreateSut(foodItemRepo: repo);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => sut.SetInventoryMinQuantityAsync(31, -1m));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static FoodTrackingService CreateSut(
        IFoodItemRepository? foodItemRepo = null,
        IMealRepository? mealRepo = null,
        IGroceryListRepository? groceryRepo = null,
        IMealHistoryRepository? historyRepo = null,
        INotionFoodClient? notionClient = null,
        IUnitOfWork? unitOfWork = null)
        => new(
            foodItemRepo ?? new FakeFoodItemRepository(),
            mealRepo ?? new FakeMealRepository(),
            groceryRepo ?? new FakeGroceryListRepository(),
            historyRepo ?? new FakeMealHistoryRepository(),
            notionClient ?? new FakeNotionFoodClient(),
            unitOfWork ?? new FakeUnitOfWork(),
            NullLogger<FoodTrackingService>.Instance);

    // ── Fakes ─────────────────────────────────────────────────────────────────

    private sealed class FakeFoodItemRepository : IFoodItemRepository
    {
        public List<FoodItem> AllItems { get; init; } = [];
        private int _nextId;

        public Task<FoodItem?> GetByNotionPageIdAsync(string notionPageId, CancellationToken cancellationToken = default)
            => Task.FromResult<FoodItem?>(AllItems.FirstOrDefault(x => x.NotionPageId == notionPageId));

        public Task<FoodItem?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult<FoodItem?>(AllItems.FirstOrDefault(x => x.Id == id));

        public Task<IReadOnlyList<FoodItem>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FoodItem>>(AllItems);

        public Task<IReadOnlyList<int>> GetAllIdsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<int>>(AllItems.Select(x => x.Id).ToList());

        public Task<IReadOnlyList<FoodItem>> SearchByNameAsync(string query, int take = 10, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FoodItem>>(AllItems
                .Where(x => x.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Take(take).ToList());

        public Task<int> CountPendingNotionSyncAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<IReadOnlyList<FoodItem>> ClaimPendingNotionSyncAsync(
            int take,
            DateTime claimedAt,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FoodItem>>([]);

        public Task AddAsync(FoodItem item, CancellationToken cancellationToken = default)
        {
            if (item.Id == 0) item.Id = ++_nextId;
            AllItems.Add(item);
            return Task.CompletedTask;
        }

        public Task<int> DeleteAllAsync(CancellationToken cancellationToken = default)
        {
            var count = AllItems.Count;
            AllItems.Clear();
            return Task.FromResult(count);
        }
    }

    private sealed class FakeMealRepository : IMealRepository
    {
        public List<Meal> AllMeals { get; init; } = [];
        public Meal? MealById { get; init; }
        public Func<IReadOnlyCollection<int>, IReadOnlyList<Meal>>? CookableFactory { get; init; }
        public bool CookableWasCalled { get; private set; }

        public Task<Meal?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(MealById);

        public Task<Meal?> GetByNotionPageIdAsync(string notionPageId, CancellationToken cancellationToken = default)
            => Task.FromResult<Meal?>(AllMeals.FirstOrDefault(m => m.NotionPageId == notionPageId));

        public Task<IReadOnlyList<Meal>> GetAllWithIngredientsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Meal>>(AllMeals);

        public Task<IReadOnlyList<Meal>> GetCookableFromInventoryAsync(IReadOnlyCollection<int> availableFoodItemIds, CancellationToken cancellationToken = default)
        {
            CookableWasCalled = true;
            var result = CookableFactory?.Invoke(availableFoodItemIds) ?? [];
            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<Meal>> GetFavouritesAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Meal>>([]);

        public Task AddAsync(Meal meal, CancellationToken cancellationToken = default)
        {
            AllMeals.Add(meal);
            return Task.CompletedTask;
        }

        public Task<int> DeleteAllAsync(CancellationToken cancellationToken = default)
        {
            var count = AllMeals.Count;
            AllMeals.Clear();
            return Task.FromResult(count);
        }
    }

    private sealed class FakeGroceryListRepository : IGroceryListRepository
    {
        public List<GroceryListItem> ActiveItems { get; init; } = [];
        public List<GroceryListItem> AllItems { get; init; } = [];
        public List<GroceryListItem> AddedItems { get; } = [];
        public IReadOnlyCollection<int> LastMarkedItemIds { get; private set; } = [];
        public IReadOnlyCollection<int> LastDeletedItemIds { get; private set; } = [];
        public DateTime? LastMarkedUpdatedAtUtc { get; private set; }
        public int MarkAllBoughtResult { get; init; }
        public int DeleteBoughtResult { get; init; }

        public Task<GroceryListItem?> GetByNotionPageIdAsync(string notionPageId, CancellationToken cancellationToken = default)
            => Task.FromResult<GroceryListItem?>(null);

        public Task<IReadOnlyList<GroceryListItem>> GetActiveAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<GroceryListItem>>(ActiveItems);

        public Task<IReadOnlyList<GroceryListItem>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<GroceryListItem>>(AllItems);

        public Task<int> CountPendingNotionSyncAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<IReadOnlyList<GroceryListItem>> ClaimPendingNotionSyncAsync(int take, DateTime claimedAt, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<GroceryListItem>>([]);

        public Task AddAsync(GroceryListItem item, CancellationToken cancellationToken = default)
        {
            AddedItems.Add(item);
            return Task.CompletedTask;
        }

        public Task<int> MarkBoughtByIdsAsync(IReadOnlyCollection<int> itemIds, DateTime updatedAtUtc, CancellationToken cancellationToken = default)
        {
            LastMarkedItemIds = itemIds.ToArray();
            LastMarkedUpdatedAtUtc = updatedAtUtc;

            var count = 0;
            foreach (var item in AllItems.Where(x => itemIds.Contains(x.Id) && !x.IsBought))
            {
                item.IsBought = true;
                item.NotionSyncStatus = FoodSyncStatus.Pending;
                item.NotionUpdatedAt = updatedAtUtc;
                count++;
            }

            return Task.FromResult(count);
        }

        public Task<int> MarkAllBoughtAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(MarkAllBoughtResult);

        public Task<int> DeleteBoughtAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(DeleteBoughtResult);

        public Task<int> DeleteByIdsAsync(IReadOnlyCollection<int> itemIds, CancellationToken cancellationToken = default)
        {
            LastDeletedItemIds = itemIds.ToArray();

            var ids = itemIds.ToHashSet();
            var deletedCount = ActiveItems.RemoveAll(item => ids.Contains(item.Id));
            if (AllItems.Count > 0)
            {
                AllItems.RemoveAll(item => ids.Contains(item.Id));
            }

            return Task.FromResult(deletedCount);
        }

        public Task<int> DeleteByIdsAnyStateAsync(IReadOnlyCollection<int> itemIds, CancellationToken cancellationToken = default)
            => DeleteByIdsAsync(itemIds, cancellationToken);
    }

    private sealed class FakeMealHistoryRepository : IMealHistoryRepository
    {
        public List<MealHistory> AddedEntries { get; } = [];
        public IReadOnlyList<MealFrequency> TopMeals { get; init; } = [];
        public IReadOnlyList<MealHistory> RangeEntries { get; init; } = [];
        public CalorieSummary Summary { get; init; } = new(DateTime.MinValue, DateTime.MaxValue, 0, 0, 0, 0, 0);
        public DateTime LastFrom { get; private set; }
        public DateTime LastTo { get; private set; }

        public Task AddAsync(MealHistory entry, CancellationToken cancellationToken = default)
        {
            AddedEntries.Add(entry);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<MealHistory>> GetRangeAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
            => Task.FromResult(RangeEntries);

        public Task<CalorieSummary> GetCalorieSummaryAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
        {
            LastFrom = from;
            LastTo = to;
            return Task.FromResult(Summary);
        }

        public Task<IReadOnlyList<MealFrequency>> GetTopMealsAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult(TopMeals);
    }

    private sealed class FakeNotionFoodClient : INotionFoodClient
    {
        public string CreatedPageId { get; init; } = "notion-fake-id";
        public bool ShouldThrow { get; init; }

        public Task<string> CreateGroceryItemAsync(string name, string? quantity, string? store, CancellationToken cancellationToken = default)
        {
            if (ShouldThrow) throw new HttpRequestException("Notion unavailable");
            return Task.FromResult(CreatedPageId);
        }

        public Task MarkGroceryItemBoughtAsync(string notionPageId, bool bought, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task UpdateInventoryItemQuantityAsync(
            string notionPageId,
            string? quantityText,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task ArchivePageAsync(string notionPageId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<NotionPage>> GetInventoryAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<NotionPage>>([]);

        public Task<IReadOnlyList<NotionPage>> GetMealPlansAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<NotionPage>>([]);

        public Task<IReadOnlyList<NotionPage>> GetGroceryListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<NotionPage>>([]);
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.FromResult(1);
        }

        public Task BeginTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CommitTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RollbackTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Dispose() { }
    }
}
