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

    // ── weekly:create ────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_WeeklyCreate_ShouldReturnCreatePrompt()
    {
        var sut = CreateSut();

        var result = await sut.HandleAsync(new ConversationAgentContext(CallbackDataConstants.Weekly.Create, []));

        Assert.Equal("food.weekly.create.prompt", result.Intent);
        Assert.Contains("What meal", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── weekly:goal ──────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_WeeklyDailyGoal_ShouldShowConsumedAndGoal()
    {
        var sut = CreateSut(); // FakeFoodTrackingService returns (goal, 800, goal-800, 40%, 3)

        var result = await sut.HandleAsync(new ConversationAgentContext(CallbackDataConstants.Weekly.DailyGoal, []));

        Assert.Equal("food.weekly.goal", result.Intent);
        Assert.Contains("800", result.Message);
        Assert.Contains("2000", result.Message);
        Assert.Contains("3", result.Message); // meals logged
    }

    [Fact]
    public async Task HandleAsync_WeeklyDailyGoal_ShouldContainProgressBar()
    {
        var sut = CreateSut();

        var result = await sut.HandleAsync(new ConversationAgentContext(CallbackDataConstants.Weekly.DailyGoal, []));

        // Progress bar uses █ (filled) and ░ (empty) characters
        Assert.True(result.Message!.Contains('█') || result.Message.Contains('░'));
    }

    // ── weekly:diversity ─────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAsync_WeeklyDiversity_ShouldReturnNoMeals_WhenEmpty()
    {
        var service = new FakeFoodTrackingService
        {
            DietDiversity = new DietDiversityDto(7, 0, 0, [], [])
        };
        var sut = CreateSut(service);

        var result = await sut.HandleAsync(new ConversationAgentContext(CallbackDataConstants.Weekly.Diversity, []));

        Assert.Equal("food.weekly.diversity", result.Intent);
        Assert.Contains("No meals", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleAsync_WeeklyDiversity_ShouldShowStats_WhenMealsExist()
    {
        var service = new FakeFoodTrackingService
        {
            DietDiversity = new DietDiversityDto(7, 3, 5, ["Pasta"], ["Pasta", "Soup", "Salad"])
        };
        var sut = CreateSut(service);

        var result = await sut.HandleAsync(new ConversationAgentContext(CallbackDataConstants.Weekly.Diversity, []));

        Assert.Equal("food.weekly.diversity", result.Intent);
        Assert.Contains("5", result.Message);  // total meals
        Assert.Contains("3", result.Message);  // unique meals
        Assert.Contains("Pasta", result.Message); // repeated meal name
    }

    // ── AddItemFromTextAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task AddItemFromTextAsync_ShouldParseSingleWordName()
    {
        var sut = CreateSut();

        var result = await sut.AddItemFromTextAsync("Milk", CancellationToken.None);

        Assert.Equal("food.shop.added", result.Intent);
        Assert.Contains("Milk", result.Message);
    }

    [Fact]
    public async Task AddItemFromTextAsync_ShouldParseNameAndQuantity()
    {
        var sut = CreateSut();

        var result = await sut.AddItemFromTextAsync("Milk 2L", CancellationToken.None);

        Assert.Equal("food.shop.added", result.Intent);
        Assert.Contains("Milk", result.Message);
        Assert.Contains("2L", result.Message);
    }

    [Fact]
    public async Task AddItemFromTextAsync_ShouldParseNameQuantityAndStore()
    {
        var sut = CreateSut();

        var result = await sut.AddItemFromTextAsync("Milk 2L SuperMart", CancellationToken.None);

        Assert.Equal("food.shop.added", result.Intent);
        Assert.Contains("Milk", result.Message);
        Assert.Contains("2L", result.Message);
        Assert.Contains("SuperMart", result.Message);
    }

    [Fact]
    public async Task AddItemFromTextAsync_ShouldJoinMultiWordStore()
    {
        var sut = CreateSut();

        var result = await sut.AddItemFromTextAsync("Eggs 12pcs Whole Foods Market", CancellationToken.None);

        Assert.Equal("food.shop.added", result.Intent);
        Assert.Contains("Whole Foods Market", result.Message);
    }

    // ── Inventory handlers ───────────────────────────────────────────────────

    [Fact]
    public async Task HandleInventoryListAsync_ShouldReturnItemList_WhenItemsExist()
    {
        var service = new FakeFoodTrackingService
        {
            InventoryItems =
            [
                new FoodItemDto(1, "Milk", "Dairy", null, null, "2L"),
                new FoodItemDto(2, "Eggs", null, null, null, null)
            ]
        };
        var sut = CreateSut(service);

        var result = await sut.HandleInventoryListAsync(CancellationToken.None);

        Assert.Equal("inventory.list", result.Intent);
        Assert.Contains("Milk", result.Message);
        Assert.Contains("Eggs", result.Message);
        Assert.Contains("[1]", result.Message);
        Assert.Contains("[2]", result.Message);
    }

    [Fact]
    public async Task HandleInventoryListAsync_ShouldReturnEmpty_WhenNoItems()
    {
        var sut = CreateSut();

        var result = await sut.HandleInventoryListAsync(CancellationToken.None);

        Assert.Equal("inventory.list.empty", result.Intent);
    }

    [Fact]
    public void HandleInventorySearchPrompt_ShouldReturnSearchPrompt()
    {
        var result = FoodTrackingConversationAgent.HandleInventorySearchPrompt();

        Assert.Equal("inventory.search.prompt", result.Intent);
        Assert.NotNull(result.Message);
    }

    [Fact]
    public async Task HandleInventorySuggestAsync_ShouldReturnEmpty_WhenNoLowStock()
    {
        var sut = CreateSut(); // empty inventory

        var result = await sut.HandleInventorySuggestAsync(CancellationToken.None);

        Assert.Equal("inventory.suggest.empty", result.Intent);
    }

    [Fact]
    public async Task HandleInventorySuggestAsync_ShouldListLowStockItems()
    {
        var service = new FakeFoodTrackingService
        {
            InventoryItems =
            [
                new FoodItemDto(1, "Eggs", null, null, null, null) { CurrentQuantity = 2m },
                new FoodItemDto(2, "Milk", null, null, null, null) { CurrentQuantity = 0.5m }
            ]
        };
        var sut = CreateSut(service);

        var result = await sut.HandleInventorySuggestAsync(CancellationToken.None);

        Assert.Equal("inventory.suggest", result.Intent);
        Assert.Contains("Eggs", result.Message);
        Assert.Contains("Milk", result.Message);
    }

    [Fact]
    public async Task HandleInventoryCartAsync_ShouldReturnAdded_WhenItemExists()
    {
        var service = new FakeFoodTrackingService
        {
            InventoryItems = [new FoodItemDto(5, "Butter", null, null, null, null)]
        };
        var sut = CreateSut(service);

        var result = await sut.HandleInventoryCartAsync(5, CancellationToken.None);

        Assert.Equal("inventory.cart.added", result.Intent);
        Assert.Contains("Butter", result.Message);
    }

    [Fact]
    public async Task HandleInventoryCartAsync_ShouldReturnNotFound_WhenItemMissing()
    {
        var sut = CreateSut(); // empty inventory

        var result = await sut.HandleInventoryCartAsync(999, CancellationToken.None);

        Assert.Equal("inventory.cart.not_found", result.Intent);
    }

    // ── CanHandle — food: and inventory: prefixes ────────────────────────────

    [Theory]
    [InlineData(CallbackDataConstants.Food.Inventory)]
    [InlineData(CallbackDataConstants.Food.Shopping)]
    [InlineData(CallbackDataConstants.Food.Menu)]
    public void CanHandle_ShouldReturnTrue_ForFoodPrefixedInput(string input)
    {
        var sut = CreateSut();
        var ctx = new ConversationAgentContext(input, [input]);
        Assert.True(sut.CanHandle(ctx));
    }

    [Theory]
    [InlineData(CallbackDataConstants.Inventory.List)]
    [InlineData(CallbackDataConstants.Inventory.Search)]
    [InlineData(CallbackDataConstants.Inventory.Suggest)]
    [InlineData("inventory:cart:42")]
    public void CanHandle_ShouldReturnTrue_ForInventoryPrefixedInput(string input)
    {
        var sut = CreateSut();
        var ctx = new ConversationAgentContext(input, [input]);
        Assert.True(sut.CanHandle(ctx));
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
        public DietDiversityDto DietDiversity { get; init; } = new(7, 0, 0, [], []);
        public int ClearedCount { get; init; }
        public IReadOnlyList<FoodItemDto> InventoryItems { get; init; } = [];

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
            => Task.FromResult(DietDiversity);

        public Task<PortionCalculationDto?> CalculatePortionsAsync(int mealId, int targetServings, CancellationToken cancellationToken = default)
            => Task.FromResult<PortionCalculationDto?>(new PortionCalculationDto("Test Meal", 2, targetServings, (decimal)targetServings / 2, []));

        public Task<IReadOnlyList<FoodItemDto>> GetAllInventoryAsync(int take = 50, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FoodItemDto>>(InventoryItems.Take(take).ToList());

        public Task<IReadOnlyList<FoodItemDto>> SearchInventoryAsync(string query, int take = 10, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FoodItemDto>>(
                InventoryItems.Where(x => x.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).Take(take).ToList());

        public Task<GroceryListItemDto> AddToShoppingFromInventoryAsync(int foodItemId, string? quantity, string? store, CancellationToken cancellationToken = default)
        {
            var item = InventoryItems.FirstOrDefault(x => x.Id == foodItemId)
                ?? throw new InvalidOperationException($"Food item {foodItemId} not found.");
            return Task.FromResult(new GroceryListItemDto(99, item.Name, quantity, null, store, false));
        }

        public Task<IReadOnlyList<FoodItemDto>> GetLowStockItemsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FoodItemDto>>(
                InventoryItems.Where(x => x.CurrentQuantity.HasValue).ToList());
    }
}
