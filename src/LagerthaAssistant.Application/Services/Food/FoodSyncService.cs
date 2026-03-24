namespace LagerthaAssistant.Application.Services.Food;

using System.Globalization;
using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Interfaces.Food;
using LagerthaAssistant.Application.Interfaces.Repositories.Food;
using LagerthaAssistant.Application.Models.Food;
using LagerthaAssistant.Domain.Entities;
using LagerthaAssistant.Domain.Enums;
using Microsoft.Extensions.Logging;

public sealed class FoodSyncService : IFoodSyncService
{
    private readonly INotionFoodClient _notionClient;
    private readonly IFoodItemRepository _foodItemRepo;
    private readonly IMealRepository _mealRepo;
    private readonly IGroceryListRepository _groceryRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<FoodSyncService> _logger;

    public FoodSyncService(
        INotionFoodClient notionClient,
        IFoodItemRepository foodItemRepo,
        IMealRepository mealRepo,
        IGroceryListRepository groceryRepo,
        IUnitOfWork unitOfWork,
        ILogger<FoodSyncService> logger)
    {
        _notionClient = notionClient;
        _foodItemRepo = foodItemRepo;
        _mealRepo = mealRepo;
        _groceryRepo = groceryRepo;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<FoodSyncSummary> SyncFromNotionAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting Notion → DB food sync");

        var inventoryUpserted = 0;
        var mealsUpserted = 0;
        var groceryUpserted = 0;
        var ingredientsLinked = 0;
        var groceryArchived = 0;
        string? lastError = null;

        try
        {
            // ── 1. Inventory → FoodItems ──────────────────────────────────
            var inventoryPages = await _notionClient.GetInventoryAsync(cancellationToken);
            _logger.LogInformation("Fetched {Count} inventory pages from Notion", inventoryPages.Count);

            // Build a Notion page ID → local FoodItem ID map for later relation linking.
            var notionIdToFoodItemId = new Dictionary<string, int>(inventoryPages.Count);

            foreach (var page in inventoryPages)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var existing = await _foodItemRepo.GetByNotionPageIdAsync(page.Id, cancellationToken);
                var notionUpdatedAt = ParseDateTime(page.LastEditedTime);

                if (existing is null)
                {
                    var item = MapToFoodItem(page, notionUpdatedAt);
                    await _foodItemRepo.AddAsync(item, cancellationToken);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                    notionIdToFoodItemId[page.Id] = item.Id;
                    inventoryUpserted++;
                }
                else
                {
                    // Notion wins on structural data if it was edited after our last sync.
                    // Also refresh icon when it becomes available in Notion, even if timestamps are equal/older.
                    var hasNewIconFromNotion =
                        !string.IsNullOrWhiteSpace(page.IconEmoji)
                        && !string.Equals(existing.IconEmoji, page.IconEmoji, StringComparison.Ordinal);

                    if (notionUpdatedAt > existing.NotionUpdatedAt)
                    {
                        UpdateFoodItem(existing, page, notionUpdatedAt);
                        await _unitOfWork.SaveChangesAsync(cancellationToken);
                        inventoryUpserted++;
                    }
                    else if (hasNewIconFromNotion)
                    {
                        // Keep local quantity/min values intact when only icon needs refresh.
                        existing.IconEmoji = page.IconEmoji;
                        await _unitOfWork.SaveChangesAsync(cancellationToken);
                        inventoryUpserted++;
                    }
                    notionIdToFoodItemId[page.Id] = existing.Id;
                }
            }

            _logger.LogInformation("Inventory sync complete: {Count} upserted", inventoryUpserted);

            // ── 2. Meal Plans → Meals + MealIngredients ───────────────────
            var mealPages = await _notionClient.GetMealPlansAsync(cancellationToken);
            _logger.LogInformation("Fetched {Count} meal plan pages from Notion", mealPages.Count);

            foreach (var page in mealPages)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var existing = await _mealRepo.GetByNotionPageIdAsync(page.Id, cancellationToken);
                var notionUpdatedAt = ParseDateTime(page.LastEditedTime);

                if (existing is null)
                {
                    var meal = MapToMeal(page, notionUpdatedAt);
                    await _mealRepo.AddAsync(meal, cancellationToken);
                    // Save first to get the generated meal ID needed for MealIngredient FKs.
                    await _unitOfWork.SaveChangesAsync(cancellationToken);

                    var linked = LinkIngredients(meal, page, notionIdToFoodItemId);
                    ingredientsLinked += linked;
                    if (linked > 0)
                        await _unitOfWork.SaveChangesAsync(cancellationToken);
                    mealsUpserted++;
                }
                else if (notionUpdatedAt > existing.NotionUpdatedAt)
                {
                    UpdateMeal(existing, page, notionUpdatedAt);
                    // Re-sync ingredients: remove stale, add new — atomically in one SaveChanges.
                    // Do NOT save between Clear() and re-link: if linking fails, no data is lost.
                    existing.Ingredients.Clear();
                    var linked = LinkIngredients(existing, page, notionIdToFoodItemId);
                    ingredientsLinked += linked;
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                    mealsUpserted++;
                }
            }

            _logger.LogInformation(
                "Meal plans sync complete: {Meals} upserted, {Ingredients} ingredient links",
                mealsUpserted,
                ingredientsLinked);

            // ── 3. Grocery List → GroceryListItems ────────────────────────
            var groceryPages = await _notionClient.GetGroceryListAsync(cancellationToken);
            _logger.LogInformation("Fetched {Count} grocery list pages from Notion", groceryPages.Count);

            if (groceryPages.Count > 0)
            {
                var firstPage = groceryPages[0];
                _logger.LogInformation(
                    "First grocery page properties: [{PropertyKeys}]",
                    string.Join(", ", firstPage.Properties.Keys));
            }

            var grocerySkipped = 0;
            foreach (var page in groceryPages)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var existing = await _groceryRepo.GetByNotionPageIdAsync(page.Id, cancellationToken);
                var notionUpdatedAt = ParseDateTime(page.LastEditedTime);
                var boughtInNotion = GetCheckbox(page, "Bought?");

                if (boughtInNotion)
                {
                    if (!IsLocalPageId(page.Id))
                    {
                        try
                        {
                            await _notionClient.ArchivePageAsync(page.Id, cancellationToken);
                            groceryArchived++;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to archive bought Notion grocery page {PageId}", page.Id);
                        }
                    }

                    if (existing is not null)
                    {
                        await _groceryRepo.DeleteByIdsAnyStateAsync([existing.Id], cancellationToken);
                    }

                    grocerySkipped++;
                    continue;
                }

                if (existing is null)
                {
                    var groceryItem = MapToGroceryListItem(page, notionUpdatedAt, notionIdToFoodItemId);
                    _logger.LogDebug(
                        "Adding grocery item: Name={Name}, IsBought={IsBought}, NotionPageId={PageId}",
                        groceryItem.Name,
                        groceryItem.IsBought,
                        groceryItem.NotionPageId);
                    await _groceryRepo.AddAsync(groceryItem, cancellationToken);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                    groceryUpserted++;
                }
                else if (notionUpdatedAt > existing.NotionUpdatedAt)
                {
                    UpdateGroceryListItem(existing, page, notionUpdatedAt, notionIdToFoodItemId);
                    _logger.LogDebug(
                        "Updated grocery item: Name={Name}, IsBought={IsBought}, NotionPageId={PageId}",
                        existing.Name,
                        existing.IsBought,
                        existing.NotionPageId);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                    groceryUpserted++;
                }
                else
                {
                    grocerySkipped++;
                }
            }

            _logger.LogInformation(
                "Grocery list sync complete: {Upserted} upserted, {Skipped} unchanged, {Archived} archived",
                groceryUpserted,
                grocerySkipped,
                groceryArchived);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Food sync from Notion failed. Inventory={Inventory}, Meals={Meals}, Grocery={Grocery}",
                inventoryUpserted,
                mealsUpserted,
                groceryUpserted);
            lastError = ex.Message;
        }

        return new FoodSyncSummary(
            inventoryUpserted,
            mealsUpserted,
            groceryUpserted,
            ingredientsLinked,
            lastError is not null,
            lastError);
    }

    public async Task<int> SyncGroceryChangesToNotionAsync(int take, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Syncing grocery changes to Notion; Take: {Take}", take);

        var items = await _groceryRepo.ClaimPendingNotionSyncAsync(
            take,
            DateTime.UtcNow,
            cancellationToken);

        var synced = 0;
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (IsLocalPageId(item.NotionPageId))
                {
                    var createdPageId = await _notionClient.CreateGroceryItemAsync(
                        item.Name,
                        item.Quantity,
                        item.Store,
                        cancellationToken);

                    if (string.IsNullOrWhiteSpace(createdPageId))
                    {
                        throw new InvalidOperationException("Notion returned an empty page ID for grocery item creation.");
                    }

                    item.NotionPageId = createdPageId.Trim();
                    _logger.LogInformation(
                        "Upgraded local grocery sync ID to Notion page ID. ItemId={ItemId}; NewNotionPageId={NotionPageId}",
                        item.Id,
                        item.NotionPageId);

                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }

                if (item.IsBought)
                {
                    await _notionClient.MarkGroceryItemBoughtAsync(item.NotionPageId, true, cancellationToken);
                    await _notionClient.ArchivePageAsync(item.NotionPageId, cancellationToken);

                    await _groceryRepo.DeleteByIdsAnyStateAsync([item.Id], cancellationToken);
                    synced++;
                    continue;
                }

                await _notionClient.MarkGroceryItemBoughtAsync(item.NotionPageId, false, cancellationToken);
                item.NotionSyncStatus = FoodSyncStatus.Synced;
                item.NotionSyncedAt = DateTime.UtcNow;
                item.NotionLastError = null;
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                synced++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to sync grocery item {Id} to Notion", item.Id);
                item.NotionSyncStatus = FoodSyncStatus.Failed;
                item.NotionLastError = ex.Message;
                item.NotionLastAttemptAt = DateTime.UtcNow;
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        return synced;
    }

    public async Task<int> SyncInventoryChangesToNotionAsync(int take, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Syncing inventory changes to Notion; Take: {Take}", take);

        var items = await _foodItemRepo.ClaimPendingNotionSyncAsync(
            take,
            DateTime.UtcNow,
            cancellationToken);

        var synced = 0;
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (IsLocalPageId(item.NotionPageId))
                {
                    throw new InvalidOperationException(
                        $"Cannot sync inventory item {item.Id} with local page ID '{item.NotionPageId}'.");
                }

                var quantityText = item.CurrentQuantity?.ToString("0.###", CultureInfo.InvariantCulture);
                await _notionClient.UpdateInventoryItemAsync(item.NotionPageId, quantityText, item.MinQuantity, cancellationToken);

                item.NotionSyncStatus = FoodSyncStatus.Synced;
                item.NotionSyncedAt = DateTime.UtcNow;
                item.NotionLastError = null;
                item.NotionUpdatedAt = DateTime.UtcNow;
                item.Quantity = quantityText;
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                synced++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to sync inventory item {Id} to Notion", item.Id);
                item.NotionSyncStatus = FoodSyncStatus.Failed;
                item.NotionLastError = ex.Message;
                item.NotionLastAttemptAt = DateTime.UtcNow;
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        return synced;
    }

    public async Task<int> ReconcileNotionGroceryOrphansAsync(
        TimeSpan? gracePeriod = null,
        CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow - (gracePeriod ?? TimeSpan.FromHours(1));
        _logger.LogInformation("Starting grocery orphan reconciliation (grace cutoff={Cutoff:u})", cutoff);

        var groceryPages = await _notionClient.GetGroceryListAsync(cancellationToken);
        var archived = 0;

        foreach (var page in groceryPages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Skip already-bought items; they are handled by normal sync flow.
            if (GetCheckbox(page, "Bought?"))
                continue;

            // Skip items that were recently edited — they may be in-flight local→Notion operations.
            var lastEdited = ParseDateTime(page.LastEditedTime);
            if (lastEdited > cutoff)
            {
                _logger.LogDebug(
                    "Reconcile: skipping recently edited grocery page {PageId} (LastEdited={LastEdited:u})",
                    page.Id,
                    lastEdited);
                continue;
            }

            // If there is a local record for this page, it is not an orphan.
            var existing = await _groceryRepo.GetByNotionPageIdAsync(page.Id, cancellationToken);
            if (existing is not null)
                continue;

            try
            {
                await _notionClient.ArchivePageAsync(page.Id, cancellationToken);
                archived++;
                _logger.LogInformation(
                    "Reconcile: archived orphaned Notion grocery page {PageId} (Name={Name})",
                    page.Id,
                    GetTitle(page, "Item Name"));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Reconcile: failed to archive orphaned Notion grocery page {PageId}", page.Id);
            }
        }

        _logger.LogInformation(
            "Grocery orphan reconciliation complete. Archived={Archived} of {Total} pages fetched",
            archived,
            groceryPages.Count);

        return archived;
    }

    // ── Mapping helpers ──────────────────────────────────────────────────────

    private static FoodItem MapToFoodItem(NotionPage page, DateTime notionUpdatedAt)
    {
        return new FoodItem
        {
            NotionPageId = page.Id,
            Name = GetTitle(page, "Item Name"),
            IconEmoji = page.IconEmoji,
            Category = GetSelect(page, "Category"),
            Store = GetSelect(page, "Store"),
            Price = GetNumber(page, "Price"),
            Quantity = GetRichText(page, "Item Quantity"),
            CurrentQuantity = GetNumberOrRichTextNumber(page, "Item Quantity"),
            MinQuantity = GetNumber(page, "Min Quantity"),
            LastAddedToCartAt = GetDate(page, "Added to Cart on"),
            NotionUpdatedAt = notionUpdatedAt,
            NotionSyncStatus = FoodSyncStatus.Synced
        };
    }

    private static void UpdateFoodItem(FoodItem item, NotionPage page, DateTime notionUpdatedAt)
    {
        item.Name = GetTitle(page, "Item Name");
        item.IconEmoji = page.IconEmoji;
        item.Category = GetSelect(page, "Category");
        item.Store = GetSelect(page, "Store");
        item.Price = GetNumber(page, "Price");
        item.Quantity = GetRichText(page, "Item Quantity");
        item.CurrentQuantity = GetNumberOrRichTextNumber(page, "Item Quantity");
        item.MinQuantity = GetNumber(page, "Min Quantity");
        item.LastAddedToCartAt = GetDate(page, "Added to Cart on");
        item.NotionUpdatedAt = notionUpdatedAt;
    }

    private static Meal MapToMeal(NotionPage page, DateTime notionUpdatedAt)
    {
        return new Meal
        {
            NotionPageId = page.Id,
            Name = GetTitle(page, "Meal Name"),
            NotionUpdatedAt = notionUpdatedAt,
            NotionSyncStatus = FoodSyncStatus.Synced
        };
    }

    private static void UpdateMeal(Meal meal, NotionPage page, DateTime notionUpdatedAt)
    {
        meal.Name = GetTitle(page, "Meal Name");
        meal.NotionUpdatedAt = notionUpdatedAt;
    }

    private static GroceryListItem MapToGroceryListItem(
        NotionPage page,
        DateTime notionUpdatedAt,
        Dictionary<string, int> notionIdToFoodItemId)
    {
        int? foodItemId = null;
        var relation = GetRelationIds(page, "Inventory");
        if (relation.Count > 0 && notionIdToFoodItemId.TryGetValue(relation[0], out var fid))
        {
            foodItemId = fid;
        }

        return new GroceryListItem
        {
            NotionPageId = page.Id,
            Name = GetTitle(page, "Item Name"),
            Quantity = GetRichText(page, "Quantity"),
            EstimatedCost = GetNumber(page, "Estimated Cost"),
            Store = GetSelect(page, "Store"),
            IsBought = GetCheckbox(page, "Bought?"),
            FoodItemId = foodItemId,
            NotionUpdatedAt = notionUpdatedAt,
            NotionSyncStatus = FoodSyncStatus.Synced
        };
    }

    private static void UpdateGroceryListItem(
        GroceryListItem item,
        NotionPage page,
        DateTime notionUpdatedAt,
        Dictionary<string, int> notionIdToFoodItemId)
    {
        item.Name = GetTitle(page, "Item Name");
        item.Quantity = GetRichText(page, "Quantity");
        item.EstimatedCost = GetNumber(page, "Estimated Cost");
        item.Store = GetSelect(page, "Store");
        item.IsBought = GetCheckbox(page, "Bought?");
        item.NotionUpdatedAt = notionUpdatedAt;

        var relation = GetRelationIds(page, "Inventory");
        if (relation.Count > 0 && notionIdToFoodItemId.TryGetValue(relation[0], out var fid))
        {
            item.FoodItemId = fid;
        }
    }

    /// <summary>
    /// Links ingredients to a meal in-memory only. Caller is responsible for calling SaveChanges.
    /// Must be called after the meal already has a valid DB-generated Id.
    /// </summary>
    private int LinkIngredients(
        Meal meal,
        NotionPage page,
        Dictionary<string, int> notionIdToFoodItemId)
    {
        var ingredientNotionIds = GetRelationIds(page, "Ingredients Needed");
        var linked = 0;

        foreach (var notionId in ingredientNotionIds)
        {
            if (!notionIdToFoodItemId.TryGetValue(notionId, out var foodItemId))
            {
                _logger.LogDebug(
                    "Ingredient Notion ID {NotionId} not found in local inventory for meal '{Meal}', skipping",
                    notionId,
                    meal.Name);
                continue;
            }

            meal.Ingredients.Add(new MealIngredient
            {
                MealId = meal.Id,
                FoodItemId = foodItemId
            });
            linked++;
        }

        return linked;
    }

    // ── Notion property extractors ───────────────────────────────────────────

    private static string GetTitle(NotionPage page, string key)
    {
        if (page.Properties.TryGetValue(key, out var prop) && prop.Title is { Count: > 0 })
        {
            return string.Concat(prop.Title.Select(t => t.PlainText)).Trim();
        }
        return string.Empty;
    }

    private static string? GetRichText(NotionPage page, string key)
    {
        if (page.Properties.TryGetValue(key, out var prop) && prop.RichText is { Count: > 0 })
        {
            var text = string.Concat(prop.RichText.Select(t => t.PlainText)).Trim();
            return string.IsNullOrEmpty(text) ? null : text;
        }
        return null;
    }

    private static string? GetSelect(NotionPage page, string key)
    {
        if (page.Properties.TryGetValue(key, out var prop) && prop.Select is not null)
        {
            return string.IsNullOrWhiteSpace(prop.Select.Name) ? null : prop.Select.Name;
        }
        return null;
    }

    private static decimal? GetNumber(NotionPage page, string key)
    {
        if (page.Properties.TryGetValue(key, out var prop))
        {
            return prop.Number;
        }
        return null;
    }

    private static decimal? GetNumberOrRichTextNumber(NotionPage page, string key)
    {
        var numeric = GetNumber(page, key);
        if (numeric.HasValue)
        {
            return numeric.Value;
        }

        var richText = GetRichText(page, key);
        if (string.IsNullOrWhiteSpace(richText))
        {
            return null;
        }

        var match = System.Text.RegularExpressions.Regex.Match(richText.Trim(), @"^(\d+(?:[.,]\d+)?)");
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

    private static bool GetCheckbox(NotionPage page, string key)
    {
        if (page.Properties.TryGetValue(key, out var prop) && prop.Checkbox.HasValue)
        {
            return prop.Checkbox.Value;
        }
        return false;
    }

    private static DateTime? GetDate(NotionPage page, string key)
    {
        if (page.Properties.TryGetValue(key, out var prop) && prop.Date is not null)
        {
            if (DateTime.TryParse(prop.Date.Start, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
                return DateTime.SpecifyKind(dt.ToUniversalTime(), DateTimeKind.Utc);
        }
        return null;
    }

    private static IReadOnlyList<string> GetRelationIds(NotionPage page, string key)
    {
        if (page.Properties.TryGetValue(key, out var prop) && prop.Relation is not null)
        {
            return prop.Relation.Select(r => r.Id).ToList();
        }
        return [];
    }

    private static DateTime ParseDateTime(string value)
    {
        if (DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
            return DateTime.SpecifyKind(dt.ToUniversalTime(), DateTimeKind.Utc);
        return DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);
    }

    private static bool IsLocalPageId(string pageId)
        => pageId.StartsWith("local:", StringComparison.OrdinalIgnoreCase);
}
