namespace LagerthaAssistant.Application.Tests.Services.Food;

using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Interfaces.Food;
using LagerthaAssistant.Application.Interfaces.Repositories.Food;
using LagerthaAssistant.Application.Models.Food;
using LagerthaAssistant.Application.Options;
using LagerthaAssistant.Application.Services.Food;
using LagerthaAssistant.Domain.Entities;
using LagerthaAssistant.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public sealed class FoodSyncServiceTests
{
    // ── SyncFromNotionAsync – Inventory ──────────────────────────────────────

    [Fact]
    public async Task SyncFromNotionAsync_ShouldInsertNewFoodItem_WhenNotInDb()
    {
        var notion = new FakeNotionFoodClient
        {
            InventoryPages = [MakePage("page-1", "2026-01-01T00:00:00Z",
                ("Item Name", Title("Chicken")),
                ("Category", Select("Meat")),
                ("Store", Select("Costco")))]
        };
        var foodRepo = new FakeFoodItemRepository();
        var sut = CreateSut(notion: notion, foodRepo: foodRepo);

        var summary = await sut.SyncFromNotionAsync();

        Assert.Equal(1, summary.InventoryUpserted);
        Assert.Single(foodRepo.Added);
        Assert.Equal("Chicken", foodRepo.Added[0].Name);
        Assert.Equal("Meat", foodRepo.Added[0].Category);
        Assert.Equal("Costco", foodRepo.Added[0].Store);
        Assert.Equal(FoodSyncStatus.Synced, foodRepo.Added[0].NotionSyncStatus);
        Assert.False(summary.HasErrors);
    }

    [Fact]
    public async Task SyncFromNotionAsync_ShouldUpdateExistingFoodItem_WhenNotionIsNewer()
    {
        var existing = new FoodItem
        {
            Id = 1,
            NotionPageId = "page-1",
            Name = "Old Name",
            NotionUpdatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        var notion = new FakeNotionFoodClient
        {
            InventoryPages = [MakePage("page-1", "2026-06-01T00:00:00Z",
                ("Item Name", Title("New Name")),
                ("Store", Select("Walmart")))]
        };
        var foodRepo = new FakeFoodItemRepository { Existing = existing };
        var sut = CreateSut(notion: notion, foodRepo: foodRepo);

        var summary = await sut.SyncFromNotionAsync();

        Assert.Equal(1, summary.InventoryUpserted);
        Assert.Equal("New Name", existing.Name);
        Assert.Equal("Walmart", existing.Store);
    }

    [Fact]
    public async Task SyncFromNotionAsync_ShouldMapCurrentAndMinQuantity_FromNotionProperties()
    {
        var notion = new FakeNotionFoodClient
        {
            InventoryPages = [MakePage("page-1", "2026-06-01T00:00:00Z",
                ("Item Name", Title("Milk")),
                ("Item Quantity", RichText("2.5L")),
                ("Min Quantity", Number(1m)))]
        };
        var foodRepo = new FakeFoodItemRepository();
        var sut = CreateSut(notion: notion, foodRepo: foodRepo);

        var summary = await sut.SyncFromNotionAsync();

        Assert.Equal(1, summary.InventoryUpserted);
        Assert.Single(foodRepo.Added);
        Assert.Equal(2.5m, foodRepo.Added[0].CurrentQuantity);
        Assert.Equal(1m, foodRepo.Added[0].MinQuantity);
    }

    [Fact]
    public async Task SyncFromNotionAsync_ShouldMapIconEmoji_FromNotionPage()
    {
        var notion = new FakeNotionFoodClient
        {
            InventoryPages = [MakePage(
                "page-1",
                "2026-06-01T00:00:00Z",
                [("Item Name", Title("Beer"))],
                iconEmoji: "🍺")]
        };
        var foodRepo = new FakeFoodItemRepository();
        var sut = CreateSut(notion: notion, foodRepo: foodRepo);

        var summary = await sut.SyncFromNotionAsync();

        Assert.Equal(1, summary.InventoryUpserted);
        Assert.Single(foodRepo.Added);
        Assert.Equal("🍺", foodRepo.Added[0].IconEmoji);
    }

    [Fact]
    public async Task SyncFromNotionAsync_ShouldKeepCurrentQuantityNull_WhenItemQuantityHasNoNumericPrefix()
    {
        var notion = new FakeNotionFoodClient
        {
            InventoryPages = [MakePage("page-1", "2026-06-01T00:00:00Z",
                ("Item Name", Title("Flour")),
                ("Item Quantity", RichText("pack")))]
        };
        var foodRepo = new FakeFoodItemRepository();
        var sut = CreateSut(notion: notion, foodRepo: foodRepo);

        var summary = await sut.SyncFromNotionAsync();

        Assert.Equal(1, summary.InventoryUpserted);
        Assert.Single(foodRepo.Added);
        Assert.Null(foodRepo.Added[0].CurrentQuantity);
    }

    [Fact]
    public async Task SyncFromNotionAsync_ShouldSkipFoodItem_WhenLocalIsNewer()
    {
        var existing = new FoodItem
        {
            Id = 1,
            NotionPageId = "page-1",
            Name = "Local Name",
            NotionUpdatedAt = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc)
        };
        var notion = new FakeNotionFoodClient
        {
            InventoryPages = [MakePage("page-1", "2025-01-01T00:00:00Z",
                ("Item Name", Title("Older Name")))]
        };
        var foodRepo = new FakeFoodItemRepository { Existing = existing };
        var sut = CreateSut(notion: notion, foodRepo: foodRepo);

        var summary = await sut.SyncFromNotionAsync();

        Assert.Equal(0, summary.InventoryUpserted);
        Assert.Equal("Local Name", existing.Name); // unchanged
    }

    [Fact]
    public async Task SyncFromNotionAsync_ShouldUpdateExistingFoodItemIcon_WhenIconIsMissingLocally()
    {
        var existing = new FoodItem
        {
            Id = 1,
            NotionPageId = "page-1",
            Name = "Beer",
            IconEmoji = null,
            CurrentQuantity = 0m,
            MinQuantity = 2m,
            Quantity = null,
            NotionUpdatedAt = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc)
        };
        var notion = new FakeNotionFoodClient
        {
            InventoryPages = [MakePage(
                "page-1",
                "2026-01-01T00:00:00Z",
                [
                    ("Item Name", Title("Beer")),
                    ("Item Quantity", RichText("1")),
                    ("Min Quantity", Number(10m))
                ],
                iconEmoji: "🍺")]
        };
        var foodRepo = new FakeFoodItemRepository { Existing = existing };
        var sut = CreateSut(notion: notion, foodRepo: foodRepo);

        var summary = await sut.SyncFromNotionAsync();

        Assert.Equal(1, summary.InventoryUpserted);
        Assert.Equal("🍺", existing.IconEmoji);
        Assert.Equal(0m, existing.CurrentQuantity);
        Assert.Equal(2m, existing.MinQuantity);
        Assert.Null(existing.Quantity);
    }

    // ── SyncFromNotionAsync – Meals ──────────────────────────────────────────

    [Fact]
    public async Task SyncFromNotionAsync_ShouldInsertNewMeal_WithIngredientLinks()
    {
        // Inventory has a food item with Notion ID "food-page-1"
        var notion = new FakeNotionFoodClient
        {
            InventoryPages = [MakePage("food-page-1", "2026-01-01T00:00:00Z",
                ("Item Name", Title("Tomato")))],
            MealPages = [MakePage("meal-page-1", "2026-01-01T00:00:00Z",
                ("Meal Name", Title("Tomato Soup")),
                ("Ingredients Needed", Relation("food-page-1")))]
        };
        // food item has id=42 after insert
        var foodRepo = new FakeFoodItemRepository { InsertedId = 42 };
        var mealRepo = new FakeMealRepository();
        var sut = CreateSut(notion: notion, foodRepo: foodRepo, mealRepo: mealRepo);

        var summary = await sut.SyncFromNotionAsync();

        Assert.Equal(1, summary.MealsUpserted);
        Assert.Equal(1, summary.IngredientsLinked);
        Assert.Single(mealRepo.Added);
        Assert.Equal("Tomato Soup", mealRepo.Added[0].Name);
        Assert.Single(mealRepo.Added[0].Ingredients);
        Assert.Equal(42, mealRepo.Added[0].Ingredients.First().FoodItemId);
    }

    [Fact]
    public async Task SyncFromNotionAsync_ShouldSkipIngredient_WhenNotionIdNotInInventory()
    {
        var notion = new FakeNotionFoodClient
        {
            MealPages = [MakePage("meal-page-1", "2026-01-01T00:00:00Z",
                ("Meal Name", Title("Mystery Dish")),
                ("Ingredients Needed", Relation("unknown-notion-id")))]
        };
        var mealRepo = new FakeMealRepository();
        var sut = CreateSut(notion: notion, mealRepo: mealRepo);

        var summary = await sut.SyncFromNotionAsync();

        Assert.Equal(1, summary.MealsUpserted);
        Assert.Equal(0, summary.IngredientsLinked); // unknown ingredient skipped
        Assert.Empty(mealRepo.Added[0].Ingredients);
    }

    [Fact]
    public async Task SyncFromNotionAsync_ShouldUpdateMeal_AndRelinkIngredients_WhenNotionIsNewer()
    {
        var existingMeal = new Meal
        {
            Id = 10,
            NotionPageId = "meal-page-1",
            Name = "Old Meal",
            NotionUpdatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Ingredients = [new MealIngredient { FoodItemId = 99 }]
        };
        var notion = new FakeNotionFoodClient
        {
            MealPages = [MakePage("meal-page-1", "2026-06-01T00:00:00Z",
                ("Meal Name", Title("Updated Meal")))]
        };
        var mealRepo = new FakeMealRepository { ExistingByNotionId = existingMeal };
        var sut = CreateSut(notion: notion, mealRepo: mealRepo);

        var summary = await sut.SyncFromNotionAsync();

        Assert.Equal(1, summary.MealsUpserted);
        Assert.Equal("Updated Meal", existingMeal.Name);
        Assert.Empty(existingMeal.Ingredients); // cleared + relinked (no new ingredients in page)
    }

    // ── DateTime UTC kind ────────────────────────────────────────────────────

    [Theory]
    [InlineData("2026-01-15T10:30:00Z")]       // explicit UTC
    [InlineData("2026-01-15T10:30:00+00:00")]  // explicit offset
    [InlineData("2026-01-15T10:30:00")]         // no timezone — was previously Unspecified
    public async Task SyncFromNotionAsync_ShouldPersistUtcKind_ForFoodItemNotionUpdatedAt_Legacy(string lastEditedTime)
    {
        var notion = new FakeNotionFoodClient
        {
            InventoryPages = [MakePage("page-1", lastEditedTime,
                ("Item Name", Title("Apple")))]
        };
        var foodRepo = new FakeFoodItemRepository();
        var sut = CreateSut(notion: notion, foodRepo: foodRepo);

        await sut.SyncFromNotionAsync();

        Assert.Equal(DateTimeKind.Utc, foodRepo.Added[0].NotionUpdatedAt.Kind);
    }

    [Theory]
    [InlineData("2026-01-15T10:30:00Z")]
    [InlineData("2026-01-15T10:30:00")]
    public async Task SyncFromNotionAsync_ShouldPersistUtcKind_ForGroceryItemNotionUpdatedAt_Legacy(string lastEditedTime)
    {
        var notion = new FakeNotionFoodClient
        {
            GroceryPages = [MakePage("grocery-1", lastEditedTime,
                ("Item Name", Title("Milk")),
                ("Bought?", Checkbox(false)))]
        };
        var groceryRepo = new FakeGroceryListRepository();
        var sut = CreateSut(notion: notion, groceryRepo: groceryRepo);

        await sut.SyncFromNotionAsync();

        Assert.Equal(DateTimeKind.Utc, groceryRepo.Added[0].NotionUpdatedAt.Kind);
    }

    [Theory]
    [InlineData("2026-01-15T10:30:00Z")]
    [InlineData("2026-01-15T10:30:00")]
    public async Task SyncFromNotionAsync_ShouldPersistUtcKind_ForMealNotionUpdatedAt_Legacy(string lastEditedTime)
    {
        var notion = new FakeNotionFoodClient
        {
            MealPages = [MakePage("meal-1", lastEditedTime,
                ("Meal Name", Title("Soup")))]
        };
        var mealRepo = new FakeMealRepository();
        var sut = CreateSut(notion: notion, mealRepo: mealRepo);

        await sut.SyncFromNotionAsync();

        Assert.Equal(DateTimeKind.Utc, mealRepo.Added[0].NotionUpdatedAt.Kind);
    }

    // ── SyncFromNotionAsync – Grocery List ───────────────────────────────────

    [Fact]
    public async Task SyncFromNotionAsync_ShouldInsertNewGroceryItem()
    {
        var notion = new FakeNotionFoodClient
        {
            GroceryPages = [MakePage("grocery-1", "2026-01-01T00:00:00Z",
                ("Item Name", Title("Apples")),
                ("Quantity", RichText("1kg")),
                ("Store", Select("Whole Foods")),
                ("Bought?", Checkbox(false)))]
        };
        var groceryRepo = new FakeGroceryListRepository();
        var sut = CreateSut(notion: notion, groceryRepo: groceryRepo);

        var summary = await sut.SyncFromNotionAsync();

        Assert.Equal(1, summary.GroceryItemsUpserted);
        Assert.Single(groceryRepo.Added);
        Assert.Equal("Apples", groceryRepo.Added[0].Name);
        Assert.Equal("1kg", groceryRepo.Added[0].Quantity);
        Assert.Equal("Whole Foods", groceryRepo.Added[0].Store);
        Assert.False(groceryRepo.Added[0].IsBought);
    }

    [Fact]
    public async Task SyncFromNotionAsync_ShouldLinkGroceryItem_ToInventoryFoodItem()
    {
        var notion = new FakeNotionFoodClient
        {
            InventoryPages = [MakePage("food-1", "2026-01-01T00:00:00Z",
                ("Item Name", Title("Milk")))],
            GroceryPages = [MakePage("grocery-1", "2026-01-01T00:00:00Z",
                ("Item Name", Title("Milk")),
                ("Inventory", Relation("food-1")),
                ("Bought?", Checkbox(false)))]
        };
        var foodRepo = new FakeFoodItemRepository { InsertedId = 55 };
        var groceryRepo = new FakeGroceryListRepository();
        var sut = CreateSut(notion: notion, foodRepo: foodRepo, groceryRepo: groceryRepo);

        await sut.SyncFromNotionAsync();

        Assert.Equal(55, groceryRepo.Added[0].FoodItemId);
    }

    [Fact]
    public async Task SyncFromNotionAsync_ShouldReturnSummaryWithError_WhenNotionThrows()
    {
        var notion = new FakeNotionFoodClient { ShouldThrowOnInventory = true };
        var sut = CreateSut(notion: notion);

        var summary = await sut.SyncFromNotionAsync();

        Assert.True(summary.HasErrors);
        Assert.NotNull(summary.LastError);
        Assert.Equal(0, summary.InventoryUpserted);
    }

    [Fact]
    public async Task SyncFromNotionAsync_ShouldReturnEmptySummary_WhenNotionReturnsNoData()
    {
        var sut = CreateSut(); // all Notion fakes return empty lists
        var summary = await sut.SyncFromNotionAsync();

        Assert.Equal(0, summary.InventoryUpserted);
        Assert.Equal(0, summary.MealsUpserted);
        Assert.Equal(0, summary.GroceryItemsUpserted);
        Assert.Equal(0, summary.IngredientsLinked);
        Assert.False(summary.HasErrors);
    }

    // ── DateTime UTC kind enforcement ────────────────────────────────────────

    [Theory]
    [InlineData("2026-01-15T10:30:00Z")]
    [InlineData("2026-01-15T10:30:00+00:00")]
    [InlineData("2026-01-15T10:30:00")]
    public async Task SyncFromNotionAsync_ShouldPersistUtcKind_ForFoodItemNotionUpdatedAt(string lastEditedTime)
    {
        var notion = new FakeNotionFoodClient
        {
            InventoryPages = [MakePage("page-1", lastEditedTime,
                ("Item Name", Title("Apple")))]
        };
        var foodRepo = new FakeFoodItemRepository();
        var sut = CreateSut(notion: notion, foodRepo: foodRepo);

        await sut.SyncFromNotionAsync();

        Assert.Equal(DateTimeKind.Utc, foodRepo.Added[0].NotionUpdatedAt.Kind);
    }

    [Theory]
    [InlineData("2026-01-15T10:30:00Z")]
    [InlineData("2026-01-15T10:30:00+00:00")]
    [InlineData("2026-01-15T10:30:00")]
    public async Task SyncFromNotionAsync_ShouldPersistUtcKind_ForGroceryItemNotionUpdatedAt(string lastEditedTime)
    {
        var notion = new FakeNotionFoodClient
        {
            GroceryPages = [MakePage("grocery-1", lastEditedTime,
                ("Item Name", Title("Milk")),
                ("Bought?", Checkbox(false)))]
        };
        var groceryRepo = new FakeGroceryListRepository();
        var sut = CreateSut(notion: notion, groceryRepo: groceryRepo);

        await sut.SyncFromNotionAsync();

        Assert.Equal(DateTimeKind.Utc, groceryRepo.Added[0].NotionUpdatedAt.Kind);
    }

    [Theory]
    [InlineData("2026-01-15T10:30:00Z")]
    [InlineData("2026-01-15T10:30:00+00:00")]
    [InlineData("2026-01-15T10:30:00")]
    public async Task SyncFromNotionAsync_ShouldPersistUtcKind_ForMealNotionUpdatedAt(string lastEditedTime)
    {
        var notion = new FakeNotionFoodClient
        {
            MealPages = [MakePage("meal-1", lastEditedTime,
                ("Meal Name", Title("Pasta")))]
        };
        var mealRepo = new FakeMealRepository();
        var sut = CreateSut(notion: notion, mealRepo: mealRepo);

        await sut.SyncFromNotionAsync();

        Assert.Equal(DateTimeKind.Utc, mealRepo.Added[0].NotionUpdatedAt.Kind);
    }

    [Theory]
    [InlineData("2026-01-15T10:30:00Z")]
    [InlineData("2026-01-15T10:30:00+00:00")]
    [InlineData("2026-01-15")]
    public async Task SyncFromNotionAsync_ShouldPersistUtcKind_ForFoodItemDateProperty(string dateString)
    {
        var notion = new FakeNotionFoodClient
        {
            InventoryPages = [MakePage("page-1", "2026-01-01T00:00:00Z",
                ("Item Name", Title("Apple")),
                ("Added to Cart on", DateProp(dateString)))]
        };
        var foodRepo = new FakeFoodItemRepository();
        var sut = CreateSut(notion: notion, foodRepo: foodRepo);

        await sut.SyncFromNotionAsync();

        Assert.NotNull(foodRepo.Added[0].LastAddedToCartAt);
        Assert.Equal(DateTimeKind.Utc, foodRepo.Added[0].LastAddedToCartAt!.Value.Kind);
    }

    // ── SyncGroceryChangesToNotionAsync ──────────────────────────────────────

    [Fact]
    public async Task SyncGroceryChangesToNotionAsync_ShouldMarkItemsSynced_WhenNotionSucceeds()
    {
        var pendingItem = new GroceryListItem
        {
            Id = 1,
            NotionPageId = "page-99",
            IsBought = true,
            NotionSyncStatus = FoodSyncStatus.Pending
        };
        var groceryRepo = new FakeGroceryListRepository { PendingItems = [pendingItem] };
        var notion = new FakeNotionFoodClient();
        var uow = new FakeUnitOfWork();
        var sut = CreateSut(notion: notion, groceryRepo: groceryRepo, unitOfWork: uow);

        var synced = await sut.SyncGroceryChangesToNotionAsync(take: 10);

        Assert.Equal(1, synced);
        Assert.Equal("page-99", notion.LastBoughtPageId);
        Assert.True(notion.LastBoughtValue);
        Assert.Equal("page-99", notion.LastArchivedPageId);
        Assert.Equal([1], groceryRepo.DeletedAnyStateIds);
    }

    [Fact]
    public async Task SyncGroceryChangesToNotionAsync_ShouldMarkItemFailed_WhenNotionThrows()
    {
        var pendingItem = new GroceryListItem
        {
            Id = 1,
            NotionPageId = "page-1",
            IsBought = true,
            NotionSyncStatus = FoodSyncStatus.Pending
        };
        var groceryRepo = new FakeGroceryListRepository { PendingItems = [pendingItem] };
        var notion = new FakeNotionFoodClient { ShouldThrowOnMarkBought = true };
        var sut = CreateSut(notion: notion, groceryRepo: groceryRepo);

        var synced = await sut.SyncGroceryChangesToNotionAsync(take: 10);

        Assert.Equal(0, synced);
        Assert.Equal(FoodSyncStatus.Failed, pendingItem.NotionSyncStatus);
        Assert.NotNull(pendingItem.NotionLastError);
    }

    [Fact]
    public async Task SyncGroceryChangesToNotionAsync_ShouldReturnZero_WhenNoPendingItems()
    {
        var sut = CreateSut();
        var synced = await sut.SyncGroceryChangesToNotionAsync(take: 10);
        Assert.Equal(0, synced);
    }

    [Fact]
    public async Task SyncGroceryChangesToNotionAsync_ShouldContinue_WhenOneItemFails()
    {
        var item1 = new GroceryListItem { Id = 1, NotionPageId = "fail-page", IsBought = true, NotionSyncStatus = FoodSyncStatus.Pending };
        var item2 = new GroceryListItem { Id = 2, NotionPageId = "ok-page", IsBought = false, NotionSyncStatus = FoodSyncStatus.Pending };
        var groceryRepo = new FakeGroceryListRepository { PendingItems = [item1, item2] };
        var notion = new FakeNotionFoodClient { FailPageId = "fail-page" };
        var sut = CreateSut(notion: notion, groceryRepo: groceryRepo);

        var synced = await sut.SyncGroceryChangesToNotionAsync(take: 10);

        Assert.Equal(1, synced);
        Assert.Equal(FoodSyncStatus.Failed, item1.NotionSyncStatus);
        Assert.Equal(FoodSyncStatus.Synced, item2.NotionSyncStatus);
    }

    [Fact]
    public async Task SyncGroceryChangesToNotionAsync_ShouldCreateNotionPage_WhenPageIdIsLocal()
    {
        var pendingItem = new GroceryListItem
        {
            Id = 10,
            NotionPageId = "local:abc",
            Name = "Milk",
            Quantity = "2L",
            Store = "ATB",
            IsBought = true,
            NotionSyncStatus = FoodSyncStatus.Pending
        };
        var groceryRepo = new FakeGroceryListRepository { PendingItems = [pendingItem] };
        var notion = new FakeNotionFoodClient { CreatedPageId = "notion-real-10" };
        var sut = CreateSut(notion: notion, groceryRepo: groceryRepo);

        var synced = await sut.SyncGroceryChangesToNotionAsync(take: 10);

        Assert.Equal(1, synced);
        Assert.Equal("notion-real-10", pendingItem.NotionPageId);
        Assert.Equal("notion-real-10", notion.LastBoughtPageId);
        Assert.Equal("notion-real-10", notion.LastArchivedPageId);
        Assert.Equal("Milk", notion.LastCreatedName);
        Assert.Equal("2L", notion.LastCreatedQuantity);
        Assert.Equal("ATB", notion.LastCreatedStore);
        Assert.Equal([10], groceryRepo.DeletedAnyStateIds);
    }

    [Fact]
    public async Task SyncGroceryChangesToNotionAsync_ShouldMarkFailed_WhenCreateFromLocalIdFails()
    {
        var pendingItem = new GroceryListItem
        {
            Id = 11,
            NotionPageId = "local:def",
            Name = "Bread",
            IsBought = true,
            NotionSyncStatus = FoodSyncStatus.Pending
        };
        var groceryRepo = new FakeGroceryListRepository { PendingItems = [pendingItem] };
        var notion = new FakeNotionFoodClient { ShouldThrowOnCreate = true };
        var sut = CreateSut(notion: notion, groceryRepo: groceryRepo);

        var synced = await sut.SyncGroceryChangesToNotionAsync(take: 10);

        Assert.Equal(0, synced);
        Assert.Equal(FoodSyncStatus.Failed, pendingItem.NotionSyncStatus);
        Assert.NotNull(pendingItem.NotionLastError);
        Assert.StartsWith("local:", pendingItem.NotionPageId, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SyncInventoryChangesToNotionAsync_ShouldPushQuantity_AndMarkItemsSynced()
    {
        var pendingItem = new FoodItem
        {
            Id = 21,
            NotionPageId = "inv-21",
            CurrentQuantity = 2.5m,
            NotionSyncStatus = FoodSyncStatus.Pending
        };
        var foodRepo = new FakeFoodItemRepository { PendingItems = [pendingItem] };
        var notion = new FakeNotionFoodClient();
        var sut = CreateSut(notion: notion, foodRepo: foodRepo);

        var synced = await sut.SyncInventoryChangesToNotionAsync(take: 10);

        Assert.Equal(1, synced);
        Assert.Equal("inv-21", notion.LastQuantityPageId);
        Assert.Equal("2.5", notion.LastQuantityValue);
        Assert.Equal(FoodSyncStatus.Synced, pendingItem.NotionSyncStatus);
        Assert.NotNull(pendingItem.NotionSyncedAt);
    }

    [Fact]
    public async Task SyncInventoryChangesToNotionAsync_ShouldMarkFailed_WhenNotionThrows()
    {
        var pendingItem = new FoodItem
        {
            Id = 22,
            NotionPageId = "inv-22",
            CurrentQuantity = 1m,
            NotionSyncStatus = FoodSyncStatus.Pending
        };
        var foodRepo = new FakeFoodItemRepository { PendingItems = [pendingItem] };
        var notion = new FakeNotionFoodClient { ShouldThrowOnUpdateInventory = true };
        var sut = CreateSut(notion: notion, foodRepo: foodRepo);

        var synced = await sut.SyncInventoryChangesToNotionAsync(take: 10);

        Assert.Equal(0, synced);
        Assert.Equal(FoodSyncStatus.Failed, pendingItem.NotionSyncStatus);
        Assert.NotNull(pendingItem.NotionLastError);
    }

    [Fact]
    public async Task SyncInventoryChangesToNotionAsync_ShouldPushMinQuantity_WhenSet()
    {
        var pendingItem = new FoodItem
        {
            Id = 23,
            NotionPageId = "inv-23",
            CurrentQuantity = 3m,
            MinQuantity = 1.5m,
            NotionSyncStatus = FoodSyncStatus.Pending
        };
        var foodRepo = new FakeFoodItemRepository { PendingItems = [pendingItem] };
        var notion = new FakeNotionFoodClient();
        var sut = CreateSut(notion: notion, foodRepo: foodRepo);

        var synced = await sut.SyncInventoryChangesToNotionAsync(take: 10);

        Assert.Equal(1, synced);
        Assert.Equal("inv-23", notion.LastQuantityPageId);
        Assert.Equal("3", notion.LastQuantityValue);
        Assert.Equal(1.5m, notion.LastMinQuantityValue);
    }

    [Fact]
    public async Task SyncInventoryChangesToNotionAsync_ShouldPassNullMinQuantity_WhenNotSet()
    {
        var pendingItem = new FoodItem
        {
            Id = 24,
            NotionPageId = "inv-24",
            CurrentQuantity = 2m,
            MinQuantity = null,
            NotionSyncStatus = FoodSyncStatus.Pending
        };
        var foodRepo = new FakeFoodItemRepository { PendingItems = [pendingItem] };
        var notion = new FakeNotionFoodClient();
        var sut = CreateSut(notion: notion, foodRepo: foodRepo);

        var synced = await sut.SyncInventoryChangesToNotionAsync(take: 10);

        Assert.Equal(1, synced);
        Assert.Null(notion.LastMinQuantityValue);
    }

    [Fact]
    public async Task SyncInventoryChangesToNotionAsync_ShouldUseNotionTimestamp_NotUtcNow()
    {
        var notionTime = new DateTime(2026, 7, 15, 10, 30, 0, DateTimeKind.Utc);
        var pendingItem = new FoodItem
        {
            Id = 25,
            NotionPageId = "inv-25",
            CurrentQuantity = 5m,
            NotionSyncStatus = FoodSyncStatus.Pending
        };
        var foodRepo = new FakeFoodItemRepository { PendingItems = [pendingItem] };
        var notion = new FakeNotionFoodClient { UpdateInventoryTimestamp = notionTime };
        var sut = CreateSut(notion: notion, foodRepo: foodRepo);

        await sut.SyncInventoryChangesToNotionAsync(take: 10);

        // NotionUpdatedAt must match the Notion response, not DateTime.UtcNow.
        Assert.Equal(notionTime, pendingItem.NotionUpdatedAt);
    }

    // ── ReconcileNotionGroceryOrphansAsync ───────────────────────────────────

    [Fact]
    public async Task ReconcileNotionGroceryOrphansAsync_ShouldArchiveOrphanedNotionItem_WhenNoLocalRecord()
    {
        var notion = new FakeNotionFoodClient
        {
            GroceryPages =
            [
                MakePage("orphan-1", "2026-01-01T00:00:00Z",
                    ("Item Name", Title("Old Milk")),
                    ("Bought?", Checkbox(false)))
            ]
        };
        var groceryRepo = new FakeGroceryListRepository();
        var sut = CreateSut(notion: notion, groceryRepo: groceryRepo);

        var archived = await sut.ReconcileNotionGroceryOrphansAsync(gracePeriod: TimeSpan.Zero);

        Assert.Equal(1, archived);
        Assert.Contains("orphan-1", notion.ArchivedPageIds);
    }

    [Fact]
    public async Task ReconcileNotionGroceryOrphansAsync_ShouldSkip_WhenLocalRecordExists()
    {
        var existing = new GroceryListItem { Id = 5, NotionPageId = "page-5", Name = "Butter" };
        var notion = new FakeNotionFoodClient
        {
            GroceryPages =
            [
                MakePage("page-5", "2026-01-01T00:00:00Z",
                    ("Item Name", Title("Butter")),
                    ("Bought?", Checkbox(false)))
            ]
        };
        var groceryRepo = new FakeGroceryListRepository { ExistingByNotionPageId = existing };
        var sut = CreateSut(notion: notion, groceryRepo: groceryRepo);

        var archived = await sut.ReconcileNotionGroceryOrphansAsync(gracePeriod: TimeSpan.Zero);

        Assert.Equal(0, archived);
        Assert.Empty(notion.ArchivedPageIds);
    }

    [Fact]
    public async Task ReconcileNotionGroceryOrphansAsync_ShouldSkip_WhenItemEditedWithinGracePeriod()
    {
        var recentTime = DateTime.UtcNow.AddMinutes(-10).ToString("yyyy-MM-ddTHH:mm:ssZ");
        var notion = new FakeNotionFoodClient
        {
            GroceryPages =
            [
                MakePage("recent-1", recentTime,
                    ("Item Name", Title("Fresh Item")),
                    ("Bought?", Checkbox(false)))
            ]
        };
        var groceryRepo = new FakeGroceryListRepository();
        var sut = CreateSut(notion: notion, groceryRepo: groceryRepo);

        // Grace period of 1 hour — the item edited 10 minutes ago should be skipped.
        var archived = await sut.ReconcileNotionGroceryOrphansAsync(gracePeriod: TimeSpan.FromHours(1));

        Assert.Equal(0, archived);
        Assert.Empty(notion.ArchivedPageIds);
    }

    [Fact]
    public async Task ReconcileNotionGroceryOrphansAsync_ShouldSkip_BoughtItems()
    {
        var notion = new FakeNotionFoodClient
        {
            GroceryPages =
            [
                MakePage("bought-1", "2026-01-01T00:00:00Z",
                    ("Item Name", Title("Already Bought")),
                    ("Bought?", Checkbox(true)))
            ]
        };
        var groceryRepo = new FakeGroceryListRepository();
        var sut = CreateSut(notion: notion, groceryRepo: groceryRepo);

        var archived = await sut.ReconcileNotionGroceryOrphansAsync(gracePeriod: TimeSpan.Zero);

        Assert.Equal(0, archived);
        Assert.Empty(notion.ArchivedPageIds);
    }

    [Fact]
    public async Task ReconcileNotionGroceryOrphansAsync_ShouldContinue_WhenArchiveFailsForOneItem()
    {
        var notion = new FakeNotionFoodClient
        {
            GroceryPages =
            [
                MakePage("fail-page", "2026-01-01T00:00:00Z",
                    ("Item Name", Title("Failing Item")),
                    ("Bought?", Checkbox(false))),
                MakePage("ok-page", "2026-01-01T00:00:00Z",
                    ("Item Name", Title("OK Item")),
                    ("Bought?", Checkbox(false)))
            ],
            FailPageId = "fail-page"
        };
        var groceryRepo = new FakeGroceryListRepository();
        var sut = CreateSut(notion: notion, groceryRepo: groceryRepo);

        var archived = await sut.ReconcileNotionGroceryOrphansAsync(gracePeriod: TimeSpan.Zero);

        Assert.Equal(1, archived);
        Assert.Contains("ok-page", notion.ArchivedPageIds);
        Assert.DoesNotContain("fail-page", notion.ArchivedPageIds);
    }

    // ── Per-field conflict protection ────────────────────────────────────────

    [Fact]
    public async Task SyncFromNotionAsync_ShouldPreserveLocalQuantities_WhenItemHasPendingStatus()
    {
        // Arrange: local item has a pending local change to CurrentQuantity=10.
        var existing = new FoodItem
        {
            Id = 1,
            NotionPageId = "page-1",
            Name = "Chicken",
            CurrentQuantity = 10m,
            MinQuantity = 2m,
            NotionUpdatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            NotionSyncStatus = FoodSyncStatus.Pending
        };
        // Notion has a newer structural edit (name changed) with an older quantity.
        var notion = new FakeNotionFoodClient
        {
            InventoryPages = [MakePage("page-1", "2026-06-01T00:00:00Z",
                ("Item Name", Title("Chicken Breast")),
                ("Item Quantity", RichText("3")),
                ("Min Quantity", Number(1m)))]
        };
        var foodRepo = new FakeFoodItemRepository { Existing = existing };
        var sut = CreateSut(notion: notion, foodRepo: foodRepo);

        await sut.SyncFromNotionAsync();

        // Structural field updated from Notion.
        Assert.Equal("Chicken Breast", existing.Name);
        // Local quantities preserved — NOT overwritten by Notion.
        Assert.Equal(10m, existing.CurrentQuantity);
        Assert.Equal(2m, existing.MinQuantity);
    }

    [Fact]
    public async Task SyncFromNotionAsync_ShouldOverwriteQuantities_WhenItemIsSynced()
    {
        // Arrange: item is already synced — no pending local changes.
        var existing = new FoodItem
        {
            Id = 1,
            NotionPageId = "page-1",
            Name = "Chicken",
            CurrentQuantity = 5m,
            MinQuantity = 1m,
            NotionUpdatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            NotionSyncStatus = FoodSyncStatus.Synced
        };
        var notion = new FakeNotionFoodClient
        {
            InventoryPages = [MakePage("page-1", "2026-06-01T00:00:00Z",
                ("Item Name", Title("Chicken Breast")),
                ("Item Quantity", RichText("3")),
                ("Min Quantity", Number(0.5m)))]
        };
        var foodRepo = new FakeFoodItemRepository { Existing = existing };
        var sut = CreateSut(notion: notion, foodRepo: foodRepo);

        await sut.SyncFromNotionAsync();

        // All fields — including quantities — should be updated from Notion.
        Assert.Equal("Chicken Breast", existing.Name);
        Assert.Equal(3m, existing.CurrentQuantity);
        Assert.Equal(0.5m, existing.MinQuantity);
    }

    // ── PermanentlyFailed status ──────────────────────────────────────────────

    [Fact]
    public async Task SyncInventoryChangesToNotionAsync_ShouldMarkPermanentlyFailed_WhenMaxAttemptsExceeded()
    {
        var pendingItem = new FoodItem
        {
            Id = 31,
            NotionPageId = "inv-31",
            CurrentQuantity = 1m,
            NotionSyncStatus = FoodSyncStatus.Pending,
            NotionAttemptCount = 5   // equals MaxSyncAttempts
        };
        var foodRepo = new FakeFoodItemRepository { PendingItems = [pendingItem] };
        var notion = new FakeNotionFoodClient { ShouldThrowOnUpdateInventory = true };
        var sut = CreateSut(notion: notion, foodRepo: foodRepo);

        await sut.SyncInventoryChangesToNotionAsync(take: 10);

        Assert.Equal(FoodSyncStatus.PermanentlyFailed, pendingItem.NotionSyncStatus);
    }

    [Fact]
    public async Task SyncInventoryChangesToNotionAsync_ShouldMarkFailed_WhenUnderMaxAttempts()
    {
        var pendingItem = new FoodItem
        {
            Id = 32,
            NotionPageId = "inv-32",
            CurrentQuantity = 1m,
            NotionSyncStatus = FoodSyncStatus.Pending,
            NotionAttemptCount = 3   // below MaxSyncAttempts=5
        };
        var foodRepo = new FakeFoodItemRepository { PendingItems = [pendingItem] };
        var notion = new FakeNotionFoodClient { ShouldThrowOnUpdateInventory = true };
        var sut = CreateSut(notion: notion, foodRepo: foodRepo);

        await sut.SyncInventoryChangesToNotionAsync(take: 10);

        Assert.Equal(FoodSyncStatus.Failed, pendingItem.NotionSyncStatus);
    }

    [Fact]
    public async Task SyncGroceryChangesToNotionAsync_ShouldMarkPermanentlyFailed_WhenMaxAttemptsExceeded()
    {
        var pendingItem = new GroceryListItem
        {
            Id = 41,
            NotionPageId = "groc-41",
            IsBought = true,
            NotionSyncStatus = FoodSyncStatus.Pending,
            NotionAttemptCount = 5
        };
        var groceryRepo = new FakeGroceryListRepository { PendingItems = [pendingItem] };
        var notion = new FakeNotionFoodClient { ShouldThrowOnMarkBought = true };
        var sut = CreateSut(notion: notion, groceryRepo: groceryRepo);

        await sut.SyncGroceryChangesToNotionAsync(take: 10);

        Assert.Equal(FoodSyncStatus.PermanentlyFailed, pendingItem.NotionSyncStatus);
    }

    [Fact]
    public async Task SyncInventoryChangesToNotionAsync_ShouldRespectConfiguredMaxSyncAttempts()
    {
        // With MaxSyncAttempts=3, an item at attempt count 3 should be permanently failed.
        var pendingItem = new FoodItem
        {
            Id = 33,
            NotionPageId = "inv-33",
            CurrentQuantity = 1m,
            NotionSyncStatus = FoodSyncStatus.Pending,
            NotionAttemptCount = 3
        };
        var foodRepo = new FakeFoodItemRepository { PendingItems = [pendingItem] };
        var notion = new FakeNotionFoodClient { ShouldThrowOnUpdateInventory = true };
        var sut = CreateSut(notion: notion, foodRepo: foodRepo, options: new FoodSyncOptions { MaxSyncAttempts = 3 });

        await sut.SyncInventoryChangesToNotionAsync(take: 10);

        Assert.Equal(FoodSyncStatus.PermanentlyFailed, pendingItem.NotionSyncStatus);
    }

    // ── GetSyncStatusAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task GetSyncStatusAsync_ShouldReturnCombinedCounts()
    {
        var foodRepo = new FakeFoodItemRepository
        {
            PendingItems = [new FoodItem { Id = 1, NotionPageId = "p1", NotionSyncStatus = FoodSyncStatus.Pending }]
        };
        var groceryRepo = new FakeGroceryListRepository
        {
            PendingItems = [new GroceryListItem { Id = 2, NotionPageId = "g1", NotionSyncStatus = FoodSyncStatus.Pending }]
        };
        var sut = CreateSut(foodRepo: foodRepo, groceryRepo: groceryRepo);

        var status = await sut.GetSyncStatusAsync();

        Assert.Equal(1, status.InventoryPendingOrFailed);
        Assert.Equal(1, status.GroceryPendingOrFailed);
        Assert.Equal(0, status.InventoryPermanentlyFailed);
        Assert.Equal(0, status.GroceryPermanentlyFailed);
    }

    [Fact]
    public async Task SyncFromNotionAsync_ShouldArchiveBoughtNotionItems_AndDeleteLocalRows()
    {
        var existing = new GroceryListItem
        {
            Id = 33,
            NotionPageId = "grocery-bought-33",
            Name = "Milk",
            IsBought = false
        };
        var notion = new FakeNotionFoodClient
        {
            GroceryPages =
            [
                MakePage("grocery-bought-33", "2026-01-01T00:00:00Z",
                    ("Item Name", Title("Milk")),
                    ("Bought?", Checkbox(true)))
            ]
        };
        var groceryRepo = new FakeGroceryListRepository { ExistingByNotionPageId = existing };
        var sut = CreateSut(notion: notion, groceryRepo: groceryRepo);

        var summary = await sut.SyncFromNotionAsync();

        Assert.False(summary.HasErrors);
        Assert.Equal(0, summary.GroceryItemsUpserted);
        Assert.Equal("grocery-bought-33", notion.LastArchivedPageId);
        Assert.Equal([33], groceryRepo.DeletedAnyStateIds);
    }

    // ── Tombstone (soft-delete guard) ────────────────────────────────────────

    [Fact]
    public async Task SyncFromNotionAsync_ShouldNotReCreateGroceryItem_WhenTombstoneExists()
    {
        // A grocery page is active in Notion but has an archived tombstone locally.
        // Pull-sync must skip it to prevent zombie re-creation.
        var notion = new FakeNotionFoodClient
        {
            GroceryPages =
            [
                MakePage("grocery-zombie-1", "2026-06-01T00:00:00Z",
                    ("Item Name", Title("Zombie Milk")))
            ]
        };
        var groceryRepo = new FakeGroceryListRepository
        {
            ArchivedNotionPageIds = { "grocery-zombie-1" }
        };
        var sut = CreateSut(notion: notion, groceryRepo: groceryRepo);

        var summary = await sut.SyncFromNotionAsync();

        Assert.False(summary.HasErrors);
        Assert.Equal(0, summary.GroceryItemsUpserted);
        Assert.Empty(groceryRepo.Added);
    }

    [Fact]
    public async Task SyncFromNotionAsync_ShouldCreateGroceryItem_WhenNoTombstoneExists()
    {
        // Normal case: page in Notion, no tombstone, no existing record → should create.
        var notion = new FakeNotionFoodClient
        {
            GroceryPages =
            [
                MakePage("grocery-new-1", "2026-06-01T00:00:00Z",
                    ("Item Name", Title("Fresh Bread")))
            ]
        };
        var groceryRepo = new FakeGroceryListRepository();
        var sut = CreateSut(notion: notion, groceryRepo: groceryRepo);

        var summary = await sut.SyncFromNotionAsync();

        Assert.Equal(1, summary.GroceryItemsUpserted);
        Assert.Single(groceryRepo.Added);
        Assert.Equal("Fresh Bread", groceryRepo.Added[0].Name);
    }

    [Fact]
    public async Task SyncFromNotionAsync_ShouldSoftDeleteBoughtGroceryItem_LeavingTombstone()
    {
        // When pull-sync encounters a bought item, it calls DeleteByIdsAnyStateAsync.
        // Verify the fake tracks the delete (real impl now soft-deletes).
        var existing = new GroceryListItem
        {
            Id = 99,
            NotionPageId = "grocery-bought-99",
            Name = "Old Juice",
            IsBought = false
        };
        var notion = new FakeNotionFoodClient
        {
            GroceryPages =
            [
                MakePage("grocery-bought-99", "2026-06-01T00:00:00Z",
                    ("Item Name", Title("Old Juice")),
                    ("Bought?", Checkbox(true)))
            ]
        };
        var groceryRepo = new FakeGroceryListRepository { ExistingByNotionPageId = existing };
        var sut = CreateSut(notion: notion, groceryRepo: groceryRepo);

        await sut.SyncFromNotionAsync();

        Assert.Equal([99], groceryRepo.DeletedAnyStateIds);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static FoodSyncService CreateSut(
        INotionFoodClient? notion = null,
        IFoodItemRepository? foodRepo = null,
        IMealRepository? mealRepo = null,
        IGroceryListRepository? groceryRepo = null,
        IUnitOfWork? unitOfWork = null,
        FoodSyncOptions? options = null)
        => new(
            notion ?? new FakeNotionFoodClient(),
            foodRepo ?? new FakeFoodItemRepository(),
            mealRepo ?? new FakeMealRepository(),
            groceryRepo ?? new FakeGroceryListRepository(),
            unitOfWork ?? new FakeUnitOfWork(),
            NullLogger<FoodSyncService>.Instance,
            options);

    // ── Notion page builders ─────────────────────────────────────────────────

    private static NotionPage MakePage(
        string id,
        string lastEditedTime,
        IReadOnlyList<(string Key, NotionPropertyValue Value)> props,
        string? iconEmoji = null)
        => new(id, lastEditedTime, props.ToDictionary(p => p.Key, p => p.Value), iconEmoji);

    private static NotionPage MakePage(
        string id,
        string lastEditedTime,
        params (string Key, NotionPropertyValue Value)[] props)
        => MakePage(id, lastEditedTime, (IReadOnlyList<(string Key, NotionPropertyValue Value)>)props);

    private static NotionPropertyValue Title(string text)
        => new("title", [new NotionRichTextItem(text)], null, null, null, null, null, null);

    private static NotionPropertyValue RichText(string text)
        => new("rich_text", null, [new NotionRichTextItem(text)], null, null, null, null, null);

    private static NotionPropertyValue Select(string name)
        => new("select", null, null, new NotionSelectValue(name), null, null, null, null);

    private static NotionPropertyValue Checkbox(bool value)
        => new("checkbox", null, null, null, null, value, null, null);

    private static NotionPropertyValue Relation(params string[] ids)
        => new("relation", null, null, null, null, null, null, ids.Select(id => new NotionRelationItem(id)).ToList());

    private static NotionPropertyValue Number(decimal value)
        => new("number", null, null, null, value, null, null, null);

    private static NotionPropertyValue DateProp(string date)
        => new("date", null, null, null, null, null, new NotionDateValue(date), null);

    // ── Fakes ─────────────────────────────────────────────────────────────────

    private sealed class FakeNotionFoodClient : INotionFoodClient
    {
        public IReadOnlyList<NotionPage> InventoryPages { get; init; } = [];
        public IReadOnlyList<NotionPage> MealPages { get; init; } = [];
        public IReadOnlyList<NotionPage> GroceryPages { get; init; } = [];
        public string CreatedPageId { get; init; } = "notion-created-id";
        public bool ShouldThrowOnInventory { get; init; }
        public bool ShouldThrowOnMarkBought { get; init; }
        public bool ShouldThrowOnUpdateInventory { get; init; }
        public bool ShouldThrowOnArchive { get; init; }
        public bool ShouldThrowOnCreate { get; init; }
        public string? FailPageId { get; init; }

        public string? LastBoughtPageId { get; private set; }
        public bool LastBoughtValue { get; private set; }
        public string? LastQuantityPageId { get; private set; }
        public string? LastQuantityValue { get; private set; }
        public decimal? LastMinQuantityValue { get; private set; }
        public List<string> ArchivedPageIds { get; } = [];
        public string? LastArchivedPageId => ArchivedPageIds.Count > 0 ? ArchivedPageIds[^1] : null;
        public string? LastCreatedName { get; private set; }
        public string? LastCreatedQuantity { get; private set; }
        public string? LastCreatedStore { get; private set; }

        public Task<IReadOnlyList<NotionPage>> GetInventoryAsync(CancellationToken cancellationToken = default)
        {
            if (ShouldThrowOnInventory) throw new HttpRequestException("Notion down");
            return Task.FromResult(InventoryPages);
        }

        public Task<IReadOnlyList<NotionPage>> GetMealPlansAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(MealPages);

        public Task<IReadOnlyList<NotionPage>> GetGroceryListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(GroceryPages);

        public Task MarkGroceryItemBoughtAsync(string notionPageId, bool bought, CancellationToken cancellationToken = default)
        {
            if (ShouldThrowOnMarkBought || notionPageId == FailPageId)
                throw new HttpRequestException("Notion error");
            LastBoughtPageId = notionPageId;
            LastBoughtValue = bought;
            return Task.CompletedTask;
        }

        public DateTime UpdateInventoryTimestamp { get; set; } = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        public Task<DateTime> UpdateInventoryItemAsync(
            string notionPageId,
            string? quantityText,
            decimal? minQuantity,
            CancellationToken cancellationToken = default)
        {
            if (ShouldThrowOnUpdateInventory || notionPageId == FailPageId)
            {
                throw new HttpRequestException("Notion quantity update failed");
            }

            LastQuantityPageId = notionPageId;
            LastQuantityValue = quantityText;
            LastMinQuantityValue = minQuantity;
            return Task.FromResult(UpdateInventoryTimestamp);
        }

        public Task ArchivePageAsync(string notionPageId, CancellationToken cancellationToken = default)
        {
            if (ShouldThrowOnArchive || notionPageId == FailPageId)
            {
                throw new HttpRequestException("Notion archive failed");
            }

            ArchivedPageIds.Add(notionPageId);
            return Task.CompletedTask;
        }

        public Task<string> CreateGroceryItemAsync(string name, string? quantity, string? store, CancellationToken cancellationToken = default)
        {
            if (ShouldThrowOnCreate)
            {
                throw new HttpRequestException("Notion create failed");
            }

            LastCreatedName = name;
            LastCreatedQuantity = quantity;
            LastCreatedStore = store;
            return Task.FromResult(CreatedPageId);
        }
    }

    private sealed class FakeFoodItemRepository : IFoodItemRepository
    {
        public FoodItem? Existing { get; init; }
        public List<FoodItem> Added { get; } = [];
        public List<FoodItem> PendingItems { get; init; } = [];
        public int InsertedId { get; init; }
        private int _nextId;

        public Task<FoodItem?> GetByNotionPageIdAsync(string notionPageId, CancellationToken cancellationToken = default)
            => Task.FromResult(Existing?.NotionPageId == notionPageId ? Existing : null);

        public Task<FoodItem?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(
                Added.Concat(Existing is null ? [] : [Existing]).FirstOrDefault(x => x.Id == id));

        public Task<IReadOnlyList<FoodItem>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FoodItem>>(Added.Concat(Existing is null ? [] : [Existing]).ToList());

        public Task<IReadOnlyList<FoodItem>> SearchByNameAsync(string query, int take = 10, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FoodItem>>([]);

        public Task<int> CountPendingNotionSyncAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(PendingItems.Count);

        public Task<int> CountPermanentlyFailedNotionSyncAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<IReadOnlyList<FoodItem>> ClaimPendingNotionSyncAsync(int take, DateTime claimedAt, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FoodItem>>(PendingItems.Take(take).ToList());

        public Task AddAsync(FoodItem item, CancellationToken cancellationToken = default)
        {
            item.Id = InsertedId > 0 ? InsertedId : ++_nextId;
            Added.Add(item);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<int>> GetAllIdsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<int>>(Added.Concat(Existing is null ? [] : [Existing]).Select(x => x.Id).ToList());

        public Task<int> DeleteAllAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
    }

    private sealed class FakeMealRepository : IMealRepository
    {
        public Meal? ExistingByNotionId { get; init; }
        public List<Meal> Added { get; } = [];

        public Task<Meal?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult<Meal?>(null);

        public Task<Meal?> GetByNotionPageIdAsync(string notionPageId, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingByNotionId?.NotionPageId == notionPageId ? ExistingByNotionId : null);

        public Task<IReadOnlyList<Meal>> GetAllWithIngredientsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Meal>>(Added);

        public Task<IReadOnlyList<Meal>> GetCookableFromInventoryAsync(IReadOnlyCollection<int> availableFoodItemIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Meal>>([]);

        public Task<IReadOnlyList<Meal>> GetFavouritesAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Meal>>([]);

        public Task AddAsync(Meal meal, CancellationToken cancellationToken = default)
        {
            Added.Add(meal);
            return Task.CompletedTask;
        }

        public Task<int> DeleteAllAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
    }

    private sealed class FakeGroceryListRepository : IGroceryListRepository
    {
        public List<GroceryListItem> Added { get; } = [];
        public List<GroceryListItem> PendingItems { get; init; } = [];
        public GroceryListItem? ExistingByNotionPageId { get; init; }
        public List<int> DeletedAnyStateIds { get; } = [];
        public HashSet<string> ArchivedNotionPageIds { get; } = [];

        public Task<GroceryListItem?> GetByNotionPageIdAsync(string notionPageId, CancellationToken cancellationToken = default)
            => Task.FromResult(
                ExistingByNotionPageId?.NotionPageId == notionPageId
                    ? ExistingByNotionPageId
                    : null);

        public Task<IReadOnlyList<GroceryListItem>> GetActiveAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<GroceryListItem>>([]);

        public Task<IReadOnlyList<GroceryListItem>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<GroceryListItem>>([]);

        public Task<int> CountPendingNotionSyncAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(PendingItems.Count);

        public Task<int> CountPermanentlyFailedNotionSyncAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<IReadOnlyList<GroceryListItem>> ClaimPendingNotionSyncAsync(int take, DateTime claimedAt, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<GroceryListItem>>(PendingItems.Take(take).ToList());

        public Task AddAsync(GroceryListItem item, CancellationToken cancellationToken = default)
        {
            Added.Add(item);
            return Task.CompletedTask;
        }

        public Task<int> MarkBoughtByIdsAsync(IReadOnlyCollection<int> itemIds, DateTime updatedAtUtc, CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<int> MarkAllBoughtAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<int> DeleteBoughtAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<int> DeleteByIdsAsync(IReadOnlyCollection<int> itemIds, CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<int> DeleteByIdsAnyStateAsync(IReadOnlyCollection<int> itemIds, CancellationToken cancellationToken = default)
        {
            DeletedAnyStateIds.AddRange(itemIds);
            return Task.FromResult(itemIds.Count);
        }

        public Task<bool> ExistsArchivedByNotionPageIdAsync(string notionPageId, CancellationToken cancellationToken = default)
            => Task.FromResult(ArchivedNotionPageIds.Contains(notionPageId));

        public Task<int> PurgeArchivedAsync(DateTime olderThan, CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task BeginTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CommitTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RollbackTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Dispose() { }
    }
}
