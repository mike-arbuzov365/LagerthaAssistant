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
        Assert.Equal(1, uow.SaveCount);
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

        public Task<FoodItem?> GetByNotionPageIdAsync(string notionPageId, CancellationToken cancellationToken = default)
            => Task.FromResult<FoodItem?>(AllItems.FirstOrDefault(x => x.NotionPageId == notionPageId));

        public Task<IReadOnlyList<FoodItem>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FoodItem>>(AllItems);

        public Task<IReadOnlyList<FoodItem>> SearchByNameAsync(string query, int take = 10, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FoodItem>>(AllItems
                .Where(x => x.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Take(take).ToList());

        public Task AddAsync(FoodItem item, CancellationToken cancellationToken = default)
        {
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

        public Task<int> MarkAllBoughtAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(MarkAllBoughtResult);

        public Task<int> DeleteBoughtAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(DeleteBoughtResult);
    }

    private sealed class FakeMealHistoryRepository : IMealHistoryRepository
    {
        public List<MealHistory> AddedEntries { get; } = [];
        public IReadOnlyList<MealFrequency> TopMeals { get; init; } = [];
        public CalorieSummary Summary { get; init; } = new(DateTime.MinValue, DateTime.MaxValue, 0, 0, 0, 0, 0);
        public DateTime LastFrom { get; private set; }
        public DateTime LastTo { get; private set; }

        public Task AddAsync(MealHistory entry, CancellationToken cancellationToken = default)
        {
            AddedEntries.Add(entry);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<MealHistory>> GetRangeAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<MealHistory>>([]);

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
