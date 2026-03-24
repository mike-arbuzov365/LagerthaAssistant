namespace LagerthaAssistant.Application.Tests.Services.Agents;

using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.Interfaces;
using LagerthaAssistant.Application.Interfaces.Food;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Models.Food;
using LagerthaAssistant.Application.Services.Agents;
using Xunit;

public sealed class FoodTrackingConversationAgentTests
{
    // â”€â”€ CanHandle â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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

    // â”€â”€ HandleAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task HandleAsync_ShopList_ShouldReturnEmptyMessage_WhenNoItems()
    {
        var service = new FakeFoodTrackingService();
        var sut = CreateSut(service);

        var result = await sut.HandleAsync(new ConversationAgentContext(CallbackDataConstants.Shop.List, []));

        Assert.Equal("food.shop.list.empty", result.Intent);
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

        Assert.Equal("food.shop.clear.done", result.Intent);
        Assert.Contains("3", result.Message);
    }

    [Fact]
    public async Task HandleAsync_ShopDelete_ShouldReportNoItems_WhenNothingToDelete()
    {
        var service = new FakeFoodTrackingService { ClearedCount = 0 };
        var sut = CreateSut(service);

        var result = await sut.HandleAsync(new ConversationAgentContext(CallbackDataConstants.Shop.Delete, []));

        Assert.Equal("food.shop.clear.none", result.Intent);
        Assert.Contains("No bought", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleAsync_WeeklyView_ShouldReturnEmptyMessage_WhenNoMeals()
    {
        var service = new FakeFoodTrackingService();
        var sut = CreateSut(service);

        var result = await sut.HandleAsync(new ConversationAgentContext(CallbackDataConstants.Weekly.View, []));

        Assert.Equal("food.weekly.view.empty", result.Intent);
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

        Assert.Equal("food.weekly.cookable.empty", result.Intent);
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

        Assert.Equal("food.weekly.calories.empty", result.Intent);
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

        Assert.Equal("food.weekly.favourites.empty", result.Intent);
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

        Assert.Equal("food.weekly.log.empty", result.Intent);
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

    // â”€â”€ weekly:create â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task HandleAsync_WeeklyCreate_ShouldReturnCreatePrompt()
    {
        var sut = CreateSut();

        var result = await sut.HandleAsync(new ConversationAgentContext(CallbackDataConstants.Weekly.Create, []));

        Assert.Equal("food.weekly.create.prompt", result.Intent);
        Assert.Contains("What meal", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    // â”€â”€ weekly:goal â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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

        // Progress bar uses block characters.
        Assert.True(result.Message!.Contains('\u2588') || result.Message.Contains('\u2591'));
    }

    // â”€â”€ weekly:diversity â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task HandleAsync_WeeklyDiversity_ShouldReturnNoMeals_WhenEmpty()
    {
        var service = new FakeFoodTrackingService
        {
            DietDiversity = new DietDiversityDto(7, 0, 0, [], [])
        };
        var sut = CreateSut(service);

        var result = await sut.HandleAsync(new ConversationAgentContext(CallbackDataConstants.Weekly.Diversity, []));

        Assert.Equal("food.weekly.diversity.empty", result.Intent);
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

    // â”€â”€ AddItemFromTextAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public async Task AddItemFromTextAsync_ShouldAddFromInventory_WhenSingleWordMatches()
    {
        var service = new FakeFoodTrackingService
        {
            InventoryItems = [new FoodItemDto(1, "Milk", null, null, null, null)]
        };
        var sut = CreateSut(service);

        var result = await sut.AddItemFromTextAsync("Milk", CancellationToken.None);

        Assert.Equal("food.shop.added", result.Intent);
        Assert.Contains("Milk", result.Message);
    }

    [Fact]
    public async Task AddItemFromTextAsync_ShouldParseNameAndQuantity_WhenInventoryMatches()
    {
        var service = new FakeFoodTrackingService
        {
            InventoryItems = [new FoodItemDto(1, "Milk", null, null, null, null)]
        };
        var sut = CreateSut(service);

        var result = await sut.AddItemFromTextAsync("Milk 2L", CancellationToken.None);

        Assert.Equal("food.shop.added", result.Intent);
        Assert.Contains("Milk", result.Message);
        Assert.Contains("2L", result.Message);
    }

    [Fact]
    public async Task AddItemFromTextAsync_ShouldParseNameQuantityAndStore_WhenInventoryMatches()
    {
        var service = new FakeFoodTrackingService
        {
            InventoryItems = [new FoodItemDto(1, "Milk", null, null, null, null)]
        };
        var sut = CreateSut(service);

        var result = await sut.AddItemFromTextAsync("Milk | qty:2L | store:SuperMart", CancellationToken.None);

        Assert.Equal("food.shop.added", result.Intent);
        Assert.Contains("Milk", result.Message);
        Assert.Contains("2L", result.Message);
        Assert.Contains("SuperMart", result.Message);
    }

    [Fact]
    public async Task AddItemFromTextAsync_ShouldJoinMultiWordStore_WhenInventoryMatches()
    {
        var service = new FakeFoodTrackingService
        {
            InventoryItems = [new FoodItemDto(2, "Eggs", null, null, null, null)]
        };
        var sut = CreateSut(service);

        var result = await sut.AddItemFromTextAsync("Eggs | qty:12pcs | store:Whole Foods Market", CancellationToken.None);

        Assert.Equal("food.shop.added", result.Intent);
        Assert.Contains("Whole Foods Market", result.Message);
    }

    [Fact]
    public async Task AddItemFromTextAsync_ShouldReturnNotInInventory_WhenInputIsAmbiguous()
    {
        var sut = CreateSut();

        var result = await sut.AddItemFromTextAsync("Milk 2L SuperMart", CancellationToken.None);

        Assert.Equal("shop.not_in_inventory", result.Intent);
        Assert.Contains("not found in inventory", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddItemFromTextAsync_ShouldRejectCyrillicNames()
    {
        var sut = CreateSut();

        var result = await sut.AddItemFromTextAsync("\u041c\u043e\u043b\u043e\u043a\u043e 2\u043b", CancellationToken.None);

        Assert.Equal("shop.only_english", result.Intent);
        Assert.Contains("must be in English", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddItemFromTextAsync_ShouldReturnNotInInventory_WhenNoInventoryMatch()
    {
        var service = new FakeFoodTrackingService
        {
            InventoryItems = [new FoodItemDto(1, "Banana", null, null, null, null)]
        };
        var sut = CreateSut(service);

        var result = await sut.AddItemFromTextAsync("Apple", CancellationToken.None);

        Assert.Equal("shop.not_in_inventory", result.Intent);
        Assert.Contains("Add this product to inventory first", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    // â”€â”€ Inventory handlers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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
                new FoodItemDto(1, "Eggs", null, null, null, null) { CurrentQuantity = 2m, MinQuantity = 6m },
                new FoodItemDto(2, "Milk", null, null, null, null) { CurrentQuantity = 0.5m, MinQuantity = 2m }
            ]
        };
        var sut = CreateSut(service);

        var result = await sut.HandleInventorySuggestAsync(CancellationToken.None);

        Assert.Equal("inventory.suggest", result.Intent);
        Assert.Contains("Eggs", result.Message);
        Assert.Contains("Milk", result.Message);
    }

    [Fact]
    public async Task HandleInventoryStatsAsync_ShouldReturnStatsSummary()
    {
        var service = new FakeFoodTrackingService
        {
            InventoryItems =
            [
                new FoodItemDto(1, "Eggs", null, null, null, null),
                new FoodItemDto(2, "Milk", null, null, null, null)
            ]
        };
        var sut = CreateSut(service);

        var result = await sut.HandleInventoryStatsAsync(CancellationToken.None);

        Assert.Equal("inventory.stats", result.Intent);
        Assert.Contains("Total items", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void HandleInventoryAdjustPrompt_ShouldReturnPrompt()
    {
        var sut = CreateSut();

        var result = sut.HandleInventoryAdjustPrompt("en");

        Assert.Equal("inventory.adjust.prompt", result.Intent);
        Assert.Contains("format", result.Message, StringComparison.OrdinalIgnoreCase);
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

    // â”€â”€ CanHandle â€” food: and inventory: prefixes â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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
    [InlineData(CallbackDataConstants.Inventory.Stats)]
    [InlineData(CallbackDataConstants.Inventory.Adjust)]
    [InlineData(CallbackDataConstants.Inventory.Suggest)]
    [InlineData("inventory:cart:42")]
    public void CanHandle_ShouldReturnTrue_ForInventoryPrefixedInput(string input)
    {
        var sut = CreateSut();
        var ctx = new ConversationAgentContext(input, [input]);
        Assert.True(sut.CanHandle(ctx));
    }

    // â”€â”€ Profile â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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

    // â”€â”€ Helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private static FoodTrackingConversationAgent CreateSut(FakeFoodTrackingService? service = null)
        => new(service ?? new FakeFoodTrackingService(), new FakeLocalizationService());

    private sealed class FakeLocalizationService : ILocalizationService
    {
        private static readonly IReadOnlyDictionary<string, string> Dict = new Dictionary<string, string>
        {
            ["food.shop.list.empty"] = "Shopping list is empty.",
            ["food.shop.list.title"] = "Shopping list ({0} items):",
            ["food.shop.list.no_store"] = "No store",
            ["food.shop.list.store"] = "ðŸ“ {0}",
            ["food.shop.list.item"] = "  â€¢ {0}{1}{2}",
            ["food.shop.add.prompt"] = "What would you like to add to the shopping list?",
            ["food.shop.clear.none"] = "No bought items to clear.",
            ["food.shop.clear.done"] = "Cleared {0} bought item(s) from the list.",
            ["food.shop.added"] = "Added \"{0}\"{1}{2} to your shopping list.",
            ["shop.not_in_inventory"] = "\"{0}\" was not found in inventory, so it was not added to shopping list.",
            ["shop.only_english"] = "Product name must be in English. Shopping list accepts inventory items only.",
            ["shop.add_inventory_first"] = "Add this product to inventory first (in English), then add it to shopping list.",
            ["food.weekly.view.empty"] = "No meals found. Add some meals to Notion Meal Plans first.",
            ["food.weekly.view.title"] = "Meal plans ({0} meals):",
            ["food.weekly.view.ingredients"] = "  Ingredients: {0}{1}",
            ["food.weekly.cookable.empty"] = "No meals can be prepared with the current inventory.",
            ["food.weekly.cookable.title"] = "You can cook right now ({0} options):",
            ["food.weekly.calories.empty"] = "No calorie data for the past 7 days.",
            ["food.weekly.calories.title"] = "ðŸ“Š Calories â€” last 7 days ({0} â€“ {1})",
            ["food.weekly.calories.total"] = "Total:   {0} kcal",
            ["food.weekly.calories.avg"] = "Average: {0:F0} kcal/day",
            ["food.weekly.calories.protein"] = "Protein: {0:F0} g",
            ["food.weekly.calories.carbs"] = "Carbs:   {0:F0} g",
            ["food.weekly.calories.fat"] = "Fat:     {0:F0} g",
            ["food.weekly.favourites.empty"] = "No meal history yet. Log your meals to build your favourites list.",
            ["food.weekly.favourites.title"] = "â­ Your top meals ({0}):",
            ["food.weekly.log.empty"] = "No meals found. Add meals in Notion Meal Plans first, then sync.",
            ["food.weekly.log.prompt"] = "Which meal did you eat? Reply with the meal ID and optional servings:",
            ["food.weekly.create.prompt"] = "What meal would you like to create?",
            ["food.weekly.goal.title"] = "ðŸŽ¯ Daily progress â€” {0}",
            ["food.weekly.goal.consumed"] = "Consumed: {0} / {1} kcal",
            ["food.weekly.goal.remaining"] = "Remaining: {0} kcal",
            ["food.weekly.goal.meals"] = "Meals logged: {0}",
            ["food.weekly.diversity.empty"] = "No meals logged in the past 7 days.",
            ["food.weekly.diversity.title"] = "ðŸ¥— Diet diversity â€” last {0} days",
            ["food.weekly.diversity.total"] = "Total meals: {0}",
            ["food.weekly.diversity.unique"] = "Unique meals: {0}",
            ["food.weekly.diversity.repeated"] = "Most repeated:",
            ["food.weekly.diversity.score"] = "Diversity score: {0:F0}% unique",
            ["inventory.cart.added"] = "Added \"{0}\" to your shopping list.",
            ["inventory.cart.not_found"] = "Item not found in inventory.",
            ["inventory.low_stock.title"] = "Low stock â€” {0} item(s) to reorder:",
            ["inventory.low_stock.item"] = "  • {0} — {1} (needs restock)",
            ["inventory.stats.title"] = "Inventory stats",
            ["inventory.stats.total_items"] = "Total items: {0}",
            ["inventory.stats.with_current"] = "With current quantity: {0}",
            ["inventory.stats.with_min"] = "With min threshold: {0}",
            ["inventory.stats.low_stock"] = "Low stock items: {0}",
            ["inventory.stats.total_current"] = "Sum of current quantity: {0}",
            ["inventory.adjust.prompt"] = "Use format: <id> +/-<amount>",
            ["inventory.adjust.hint"] = "Example: 42 -1",
            ["food.unknown"] = "Use the buttons to navigate Shopping or Weekly Menu.",
        };

        public string Get(string key, string locale)
            => Dict.TryGetValue(key, out var v) ? v : $"[{key}]";

        public string GetLocaleForUser(string? telegramLanguageCode) => "en";
    }

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

        public Task<IReadOnlyList<FoodItemDto>> GetAllInventoryAsync(int take = 50, CancellationToken cancellationToken = default)
        {
            if (take <= 0)
            {
                return Task.FromResult<IReadOnlyList<FoodItemDto>>(InventoryItems.ToList());
            }

            return Task.FromResult<IReadOnlyList<FoodItemDto>>(InventoryItems.Take(take).ToList());
        }

        public Task<IReadOnlyList<FoodItemDto>> SearchInventoryAsync(string query, int take = 10, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return GetAllInventoryAsync(take, cancellationToken);
            }

            var filtered = InventoryItems
                .Where(x => x.Name.Contains(query, StringComparison.OrdinalIgnoreCase));

            var matches = take <= 0
                ? filtered.ToList()
                : filtered.Take(take).ToList();
            return Task.FromResult<IReadOnlyList<FoodItemDto>>(matches);
        }

        public Task<InventoryStatsDto> GetInventoryStatsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new InventoryStatsDto(InventoryItems.Count, 0, 0, 0, 0m));

        public Task<FoodItemDto> AdjustInventoryQuantityAsync(int foodItemId, decimal delta, CancellationToken cancellationToken = default)
        {
            var found = InventoryItems.FirstOrDefault(x => x.Id == foodItemId)
                ?? throw new InvalidOperationException($"Food item {foodItemId} not found in inventory.");

            return Task.FromResult(found with { CurrentQuantity = (found.CurrentQuantity ?? 0m) + delta });
        }

        public Task<FoodItemDto> SetInventoryCurrentQuantityAsync(int foodItemId, decimal quantity, CancellationToken cancellationToken = default)
        {
            var found = InventoryItems.FirstOrDefault(x => x.Id == foodItemId)
                ?? throw new InvalidOperationException($"Food item {foodItemId} not found in inventory.");

            return Task.FromResult(found with { CurrentQuantity = quantity });
        }

        public Task<int> ResetAllInventoryCurrentQuantitiesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<FoodItemDto> SetInventoryMinQuantityAsync(int foodItemId, decimal minQuantity, CancellationToken cancellationToken = default)
        {
            var found = InventoryItems.FirstOrDefault(x => x.Id == foodItemId)
                ?? throw new InvalidOperationException($"Food item {foodItemId} not found in inventory.");

            return Task.FromResult(found with { MinQuantity = minQuantity });
        }

        public Task<GroceryListItemDto> AddToShoppingFromInventoryAsync(int foodItemId, string? quantity, string? store, CancellationToken cancellationToken = default)
        {
            var found = InventoryItems.FirstOrDefault(x => x.Id == foodItemId);
            if (found is null)
            {
                throw new InvalidOperationException($"Food item {foodItemId} not found in inventory.");
            }

            return Task.FromResult(new GroceryListItemDto(100 + foodItemId, found.Name, quantity, null, store, false));
        }

        public Task<IReadOnlyList<FoodItemDto>> GetLowStockItemsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FoodItemDto>>(
                InventoryItems
                    .Where(x => x.MinQuantity.HasValue && x.CurrentQuantity.HasValue && x.CurrentQuantity.Value < x.MinQuantity.Value)
                    .ToList());

        public Task<GroceryListItemDto> AddGroceryItemAsync(string name, string? quantity, string? store, CancellationToken cancellationToken = default)
            => Task.FromResult(new GroceryListItemDto(99, name, quantity, null, store, false));

        public Task<int> MarkItemsBoughtAsync(IReadOnlyCollection<int> itemIds, CancellationToken cancellationToken = default)
            => Task.FromResult(itemIds.Count);

        public Task<int> MarkAllBoughtAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<int> ClearBoughtItemsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(ClearedCount);

        public Task<int> DeleteItemsByIdsAsync(IReadOnlyCollection<int> itemIds, CancellationToken cancellationToken = default)
            => Task.FromResult(itemIds.Count);

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
            => Task.FromResult(DietDiversity with { DaysAnalyzed = days });

        public Task<PortionCalculationDto?> CalculatePortionsAsync(int mealId, int targetServings, CancellationToken cancellationToken = default)
            => Task.FromResult<PortionCalculationDto?>(new PortionCalculationDto("Test Meal", 2, targetServings, (decimal)targetServings / 2, []));
    }
}
