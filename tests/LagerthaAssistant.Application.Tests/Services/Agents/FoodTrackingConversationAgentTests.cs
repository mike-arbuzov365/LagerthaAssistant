namespace LagerthaAssistant.Application.Tests.Services.Agents;

using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.Interfaces.Food;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Models.Food;
using LagerthaAssistant.Application.Services.Agents;
using Xunit;

public sealed class FoodTrackingConversationAgentTests
{
    // ── CanHandle ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(CallbackDataConstants.Shop.List)]
    [InlineData(CallbackDataConstants.Shop.Add)]
    [InlineData(CallbackDataConstants.Shop.Delete)]
    [InlineData("shop:unknown")]
    public void CanHandle_ShouldReturnTrue_ForShopPrefixedInput(string input)
    {
        var sut = CreateSut();
        Assert.True(sut.CanHandle(new ConversationAgentContext(input, [])));
    }

    [Theory]
    [InlineData(CallbackDataConstants.Weekly.View)]
    [InlineData(CallbackDataConstants.Weekly.Plan)]
    [InlineData(CallbackDataConstants.Weekly.Calories)]
    [InlineData("weekly:unknown")]
    public void CanHandle_ShouldReturnTrue_ForWeeklyPrefixedInput(string input)
    {
        var sut = CreateSut();
        Assert.True(sut.CanHandle(new ConversationAgentContext(input, [])));
    }

    [Theory]
    [InlineData("milk")]
    [InlineData("/food")]
    [InlineData("")]
    [InlineData("vocab:add")]
    public void CanHandle_ShouldReturnFalse_ForNonFoodInput(string input)
    {
        var sut = CreateSut();
        Assert.False(sut.CanHandle(new ConversationAgentContext(input, [])));
    }

    // ── HandleAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_ShopList_ShouldReturnEmptyMessage_WhenNoItems()
    {
        var service = new FakeFoodTrackingService();
        var sut = CreateSut(service);

        var result = await sut.HandleAsync(new ConversationAgentContext(CallbackDataConstants.Shop.List, []));

        Assert.Equal("food.shop.list", result.Intent);
        Assert.Contains("empty", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleAsync_ShopList_ShouldListItems_WhenItemsExist()
    {
        var service = new FakeFoodTrackingService
        {
            GroceryItems =
            [
                new GroceryListItemDto(1, "Milk", "2L", null, "Costco", false),
                new GroceryListItemDto(2, "Eggs", null, 5.99m, "Costco", false),
                new GroceryListItemDto(3, "Bread", null, null, null, false)
            ]
        };
        var sut = CreateSut(service);

        var result = await sut.HandleAsync(new ConversationAgentContext(CallbackDataConstants.Shop.List, []));

        Assert.Equal("food.shop.list", result.Intent);
        Assert.Contains("Milk", result.Message);
        Assert.Contains("Eggs", result.Message);
        Assert.Contains("Bread", result.Message);
        Assert.Contains("Costco", result.Message);
        Assert.Contains("3 items", result.Message);
    }

    [Fact]
    public async Task HandleAsync_ShopAdd_ShouldReturnAddPrompt()
    {
        var sut = CreateSut();

        var result = await sut.HandleAsync(new ConversationAgentContext(CallbackDataConstants.Shop.Add, []));

        Assert.Equal("food.shop.add.prompt", result.Intent);
        Assert.Contains("shopping list", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleAsync_ShopDelete_ShouldReportCount_WhenItemsCleared()
    {
        var service = new FakeFoodTrackingService { ClearedCount = 3 };
        var sut = CreateSut(service);

        var result = await sut.HandleAsync(new ConversationAgentContext(CallbackDataConstants.Shop.Delete, []));

        Assert.Equal("food.shop.clear", result.Intent);
        Assert.Contains("3", result.Message);
    }

    [Fact]
    public async Task HandleAsync_ShopDelete_ShouldReportNoItems_WhenNothingToDelete()
    {
        var service = new FakeFoodTrackingService { ClearedCount = 0 };
        var sut = CreateSut(service);

        var result = await sut.HandleAsync(new ConversationAgentContext(CallbackDataConstants.Shop.Delete, []));

        Assert.Equal("food.shop.clear", result.Intent);
        Assert.Contains("No bought", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleAsync_WeeklyView_ShouldReturnEmptyMessage_WhenNoMeals()
    {
        var service = new FakeFoodTrackingService();
        var sut = CreateSut(service);

        var result = await sut.HandleAsync(new ConversationAgentContext(CallbackDataConstants.Weekly.View, []));

        Assert.Equal("food.weekly.view", result.Intent);
        Assert.Contains("No meals", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleAsync_WeeklyView_ShouldListMeals_WhenMealsExist()
    {
        var service = new FakeFoodTrackingService
        {
            Meals =
            [
                new MealDto(1, "Borsch", 350, null, null, null, 45, 4, []),
                new MealDto(2, "Olivier", 450, null, null, null, null, 6, [])
            ]
        };
        var sut = CreateSut(service);

        var result = await sut.HandleAsync(new ConversationAgentContext(CallbackDataConstants.Weekly.View, []));

        Assert.Equal("food.weekly.view", result.Intent);
        Assert.Contains("Borsch", result.Message);
        Assert.Contains("Olivier", result.Message);
        Assert.Contains("2 meals", result.Message);
    }

    [Fact]
    public async Task HandleAsync_WeeklyPlan_ShouldReturnNoCookable_WhenInventoryEmpty()
    {
        var service = new FakeFoodTrackingService();
        var sut = CreateSut(service);

        var result = await sut.HandleAsync(new ConversationAgentContext(CallbackDataConstants.Weekly.Plan, []));

        Assert.Equal("food.weekly.cookable", result.Intent);
        Assert.Contains("No meals can", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleAsync_WeeklyPlan_ShouldListCookableMeals_WhenInventoryHasItems()
    {
        var service = new FakeFoodTrackingService
        {
            CookableMeals = [new MealDto(1, "Pasta", 600, null, null, null, 20, 2, [])]
        };
        var sut = CreateSut(service);

        var result = await sut.HandleAsync(new ConversationAgentContext(CallbackDataConstants.Weekly.Plan, []));

        Assert.Equal("food.weekly.cookable", result.Intent);
        Assert.Contains("Pasta", result.Message);
    }

    [Fact]
    public async Task HandleAsync_WeeklyCalories_ShouldReturnNoData_WhenNoHistory()
    {
        var service = new FakeFoodTrackingService();
        var sut = CreateSut(service);

        var result = await sut.HandleAsync(new ConversationAgentContext(CallbackDataConstants.Weekly.Calories, []));

        Assert.Equal("food.weekly.calories", result.Intent);
        Assert.Contains("No calorie data", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleAsync_WeeklyCalories_ShouldSummarize_WhenHistoryExists()
    {
        var service = new FakeFoodTrackingService
        {
            CalorieSummary = new CalorieSummary(DateTime.UtcNow.AddDays(-7), DateTime.UtcNow, 14000, 2000, 700, 1800, 560)
        };
        var sut = CreateSut(service);

        var result = await sut.HandleAsync(new ConversationAgentContext(CallbackDataConstants.Weekly.Calories, []));

        Assert.Equal("food.weekly.calories", result.Intent);
        Assert.Contains("14000", result.Message);
        Assert.Contains("2000", result.Message);
        Assert.Contains("Protein", result.Message);
    }

    [Fact]
    public async Task HandleAsync_UnknownCallback_ShouldReturnFallback()
    {
        var sut = CreateSut();

        var result = await sut.HandleAsync(new ConversationAgentContext("shop:mystery", []));

        Assert.Equal("food.unknown", result.Intent);
    }

    [Fact]
    public async Task HandleAsync_WeeklyFavourites_ShouldReturnNoHistory_WhenEmpty()
    {
        var sut = CreateSut();

        var result = await sut.HandleAsync(new ConversationAgentContext(CallbackDataConstants.Weekly.Favourites, []));

        Assert.Equal("food.weekly.favourites", result.Intent);
        Assert.Contains("No meal history", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleAsync_WeeklyFavourites_ShouldListMeals_WhenHistoryExists()
    {
        var service = new FakeFoodTrackingService
        {
            FavouriteMeals =
            [
                new MealFrequency(1, "Pasta", 7, new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)),
                new MealFrequency(2, "Soup", 3, null)
            ]
        };
        var sut = CreateSut(service);

        var result = await sut.HandleAsync(new ConversationAgentContext(CallbackDataConstants.Weekly.Favourites, []));

        Assert.Equal("food.weekly.favourites", result.Intent);
        Assert.Contains("Pasta", result.Message);
        Assert.Contains("7", result.Message);
        Assert.Contains("Soup", result.Message);
        Assert.Contains("3", result.Message);
    }

    [Fact]
    public async Task HandleAsync_WeeklyLog_ShouldReturnNoMeals_WhenEmpty()
    {
        var sut = CreateSut();

        var result = await sut.HandleAsync(new ConversationAgentContext(CallbackDataConstants.Weekly.Log, []));

        Assert.Equal("food.weekly.log.prompt", result.Intent);
        Assert.Contains("No meals found", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleAsync_WeeklyLog_ShouldShowMealListWithIds_WhenMealsExist()
    {
        var service = new FakeFoodTrackingService
        {
            Meals =
            [
                new MealDto(5, "Borsch", 350, null, null, null, 45, 2, []),
                new MealDto(12, "Pasta", 600, null, null, null, 20, 1, [])
            ]
        };
        var sut = CreateSut(service);

        var result = await sut.HandleAsync(new ConversationAgentContext(CallbackDataConstants.Weekly.Log, []));

        Assert.Equal("food.weekly.log.prompt", result.Intent);
        Assert.Contains("[5]", result.Message);
        Assert.Contains("Borsch", result.Message);
        Assert.Contains("[12]", result.Message);
        Assert.Contains("Pasta", result.Message);
    }

    // ── Profile ──────────────────────────────────────────────────────────────

    [Fact]
    public void Profile_ShouldHaveCorrectMetadata()
    {
        var sut = CreateSut();
        Assert.Equal("food-tracking-agent", sut.Name);
        Assert.Equal(50, sut.Order);
        Assert.Equal(ConversationAgentRole.Food, sut.Role);
        Assert.False(sut.SupportsSlashCommands);
        Assert.False(sut.SupportsBatchInputs);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static FoodTrackingConversationAgent CreateSut(FakeFoodTrackingService? service = null)
        => new(service ?? new FakeFoodTrackingService());

    private sealed class FakeFoodTrackingService : IFoodTrackingService
    {
        public IReadOnlyList<GroceryListItemDto> GroceryItems { get; init; } = [];
        public IReadOnlyList<MealDto> Meals { get; init; } = [];
        public IReadOnlyList<MealDto> CookableMeals { get; init; } = [];
        public IReadOnlyList<MealFrequency> FavouriteMeals { get; init; } = [];
        public CalorieSummary CalorieSummary { get; init; } = new(DateTime.MinValue, DateTime.MaxValue, 0, 0, 0, 0, 0);
        public int ClearedCount { get; init; }

        public Task<IReadOnlyList<GroceryListItemDto>> GetActiveGroceryListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(GroceryItems);

        public Task<GroceryListItemDto> AddGroceryItemAsync(string name, string? quantity, string? store, CancellationToken cancellationToken = default)
            => Task.FromResult(new GroceryListItemDto(99, name, quantity, null, store, false));

        public Task<int> MarkItemsBoughtAsync(IReadOnlyCollection<int> itemIds, CancellationToken cancellationToken = default)
            => Task.FromResult(itemIds.Count);

        public Task<int> MarkAllBoughtAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<int> ClearBoughtItemsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(ClearedCount);

        public Task<IReadOnlyList<MealDto>> GetAllMealsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Meals);

        public Task<IReadOnlyList<MealDto>> GetCookableNowAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CookableMeals);

        public Task<IReadOnlyList<MealFrequency>> GetFavouriteMealsAsync(int take = 5, CancellationToken cancellationToken = default)
            => Task.FromResult(FavouriteMeals);

        public Task<int> LogMealAsync(int mealId, decimal servings, string? notes, CancellationToken cancellationToken = default)
            => Task.FromResult(1);

        public Task<CalorieSummary> GetCalorieSummaryAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
            => Task.FromResult(CalorieSummary);

        public Task<MealDto> CreateMealAsync(string name, int? caloriesPerServing, decimal? proteinGrams, decimal? carbsGrams, decimal? fatGrams, int? prepTimeMinutes, int defaultServings, IReadOnlyList<(string Name, string? Quantity)> ingredients, CancellationToken cancellationToken = default)
            => Task.FromResult(new MealDto(99, name, caloriesPerServing, proteinGrams, carbsGrams, fatGrams, prepTimeMinutes, defaultServings, []));

        public Task<int> LogQuickMealAsync(string name, int calories, decimal servings, CancellationToken cancellationToken = default)
            => Task.FromResult(1);

        public Task<DailyProgressDto> GetDailyProgressAsync(int calorieGoal, CancellationToken cancellationToken = default)
            => Task.FromResult(new DailyProgressDto(calorieGoal, 800, calorieGoal - 800, 40m, 3));

        public Task<DietDiversityDto> GetDietDiversityAsync(int days = 7, CancellationToken cancellationToken = default)
            => Task.FromResult(new DietDiversityDto(days, 4, 10, ["Oatmeal"], ["Oatmeal", "Salad", "Pasta", "Soup"]));

        public Task<PortionCalculationDto?> CalculatePortionsAsync(int mealId, int targetServings, CancellationToken cancellationToken = default)
            => Task.FromResult<PortionCalculationDto?>(new PortionCalculationDto("Test Meal", 2, targetServings, (decimal)targetServings / 2, []));
    }
}
