namespace LagerthaAssistant.Application.Services.Agents;

using System.Text;
using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.Interfaces.Agents;
using LagerthaAssistant.Application.Interfaces.Food;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Models.Food;

/// <summary>
/// Handles food/shopping/meal callback actions triggered by Telegram inline keyboard buttons.
/// Activated only by callback data with shop: or weekly: prefixes.
/// Natural-language text input in Shopping/WeeklyMenu sections is handled directly in TelegramController.
/// Order 50 — executes after CommandAgent (10) but before VocabularyAgent (100).
/// </summary>
public sealed class FoodTrackingConversationAgent : IConversationAgent, IConversationAgentProfile
{
    private readonly IFoodTrackingService _foodService;

    public FoodTrackingConversationAgent(IFoodTrackingService foodService)
    {
        _foodService = foodService;
    }

    public string Name => "food-tracking-agent";

    public int Order => 50;

    public ConversationAgentRole Role => ConversationAgentRole.Food;

    public bool SupportsSlashCommands => false;

    public bool SupportsBatchInputs => false;

    public bool CanHandle(ConversationAgentContext context)
    {
        var input = context.Input;

        return input.StartsWith(CallbackDataConstants.Shop.Prefix, StringComparison.Ordinal)
            || input.StartsWith(CallbackDataConstants.Weekly.Prefix, StringComparison.Ordinal);
    }

    public async Task<ConversationAgentResult> HandleAsync(
        ConversationAgentContext context,
        CancellationToken cancellationToken = default)
    {
        var input = context.Input.Trim();

        return input switch
        {
            CallbackDataConstants.Shop.List => await HandleShopListAsync(cancellationToken),
            CallbackDataConstants.Shop.Add => HandleShopAddPrompt(),
            CallbackDataConstants.Shop.Delete => await HandleShopClearAsync(cancellationToken),
            CallbackDataConstants.Weekly.View => await HandleWeeklyViewAsync(cancellationToken),
            CallbackDataConstants.Weekly.Plan => await HandleCookableNowAsync(cancellationToken),
            CallbackDataConstants.Weekly.Calories => await HandleWeeklyCaloriesAsync(cancellationToken),
            CallbackDataConstants.Weekly.Favourites => await HandleFavouriteMealsAsync(cancellationToken),
            CallbackDataConstants.Weekly.Log => await HandleLogMealPromptAsync(cancellationToken),
            CallbackDataConstants.Weekly.Create => HandleMealCreatePrompt(),
            CallbackDataConstants.Weekly.DailyGoal => await HandleDailyGoalAsync(cancellationToken),
            CallbackDataConstants.Weekly.Diversity => await HandleDietDiversityAsync(cancellationToken),
            _ => Result("food.unknown", "Use the buttons to navigate Shopping or Weekly Menu.")
        };
    }

    // ── Handlers ─────────────────────────────────────────────────────────────

    internal async Task<ConversationAgentResult> HandleShopListAsync(CancellationToken cancellationToken)
    {
        var items = await _foodService.GetActiveGroceryListAsync(cancellationToken);

        if (items.Count == 0)
        {
            return Result("food.shop.list", "Shopping list is empty.");
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Shopping list ({items.Count} items):");
        sb.AppendLine();

        var byStore = items
            .GroupBy(x => x.Store ?? "No store")
            .OrderBy(g => g.Key);

        foreach (var group in byStore)
        {
            sb.AppendLine($"📍 {group.Key}");
            foreach (var item in group)
            {
                var qty = item.Quantity is not null ? $" — {item.Quantity}" : string.Empty;
                var cost = item.EstimatedCost.HasValue ? $" (~${item.EstimatedCost:F2})" : string.Empty;
                sb.AppendLine($"  • {item.Name}{qty}{cost}");
            }
            sb.AppendLine();
        }

        return Result("food.shop.list", sb.ToString().TrimEnd());
    }

    internal static ConversationAgentResult HandleShopAddPrompt()
    {
        return Result(
            "food.shop.add.prompt",
            "What would you like to add to the shopping list? Type the item name (and optionally quantity and store, e.g. \"Milk 2L SuperMart\").");
    }

    internal async Task<ConversationAgentResult> HandleShopClearAsync(CancellationToken cancellationToken)
    {
        var deleted = await _foodService.ClearBoughtItemsAsync(cancellationToken);
        return deleted == 0
            ? Result("food.shop.clear", "No bought items to clear.")
            : Result("food.shop.clear", $"Cleared {deleted} bought item(s) from the list.");
    }

    internal async Task<ConversationAgentResult> HandleWeeklyViewAsync(CancellationToken cancellationToken)
    {
        var meals = await _foodService.GetAllMealsAsync(cancellationToken);

        if (meals.Count == 0)
        {
            return Result("food.weekly.view", "No meals found. Add some meals to Notion Meal Plans first.");
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Meal plans ({meals.Count} meals):");
        sb.AppendLine();

        foreach (var meal in meals)
        {
            var calories = meal.CaloriesPerServing.HasValue ? $" — {meal.CaloriesPerServing} kcal/serving" : string.Empty;
            var prep = meal.PrepTimeMinutes.HasValue ? $" ({meal.PrepTimeMinutes} min)" : string.Empty;
            sb.AppendLine($"🍽 {meal.Name}{calories}{prep}");

            if (meal.Ingredients.Count > 0)
            {
                var ingredientNames = meal.Ingredients.Take(5).Select(i => i.Name);
                sb.AppendLine($"  Ingredients: {string.Join(", ", ingredientNames)}{(meal.Ingredients.Count > 5 ? "..." : string.Empty)}");
            }
        }

        return Result("food.weekly.view", sb.ToString().TrimEnd());
    }

    internal async Task<ConversationAgentResult> HandleCookableNowAsync(CancellationToken cancellationToken)
    {
        var meals = await _foodService.GetCookableNowAsync(cancellationToken);

        if (meals.Count == 0)
        {
            return Result(
                "food.weekly.cookable",
                "No meals can be prepared with the current inventory. Try syncing from Notion or adding items to your inventory.");
        }

        var sb = new StringBuilder();
        sb.AppendLine($"You can cook right now ({meals.Count} options):");
        sb.AppendLine();

        foreach (var meal in meals)
        {
            var calories = meal.CaloriesPerServing.HasValue ? $" — {meal.CaloriesPerServing} kcal/serving" : string.Empty;
            sb.AppendLine($"✅ {meal.Name}{calories}");
        }

        return Result("food.weekly.cookable", sb.ToString().TrimEnd());
    }

    internal async Task<ConversationAgentResult> HandleWeeklyCaloriesAsync(CancellationToken cancellationToken)
    {
        var to = DateTime.UtcNow;
        var from = to.AddDays(-7).Date;
        var summary = await _foodService.GetCalorieSummaryAsync(from, to, cancellationToken);

        if (summary.TotalCalories == 0)
        {
            return Result(
                "food.weekly.calories",
                "No calorie data for the past 7 days. Use /meal log to record what you eat.");
        }

        var sb = new StringBuilder();
        sb.AppendLine($"📊 Calories — last 7 days ({from:MMM d} – {to:MMM d})");
        sb.AppendLine();
        sb.AppendLine($"Total:   {summary.TotalCalories} kcal");
        sb.AppendLine($"Average: {summary.AvgCaloriesPerDay:F0} kcal/day");
        sb.AppendLine();
        sb.AppendLine($"Protein: {summary.TotalProteinGrams:F0} g");
        sb.AppendLine($"Carbs:   {summary.TotalCarbsGrams:F0} g");
        sb.AppendLine($"Fat:     {summary.TotalFatGrams:F0} g");

        return Result("food.weekly.calories", sb.ToString().TrimEnd());
    }

    internal async Task<ConversationAgentResult> HandleFavouriteMealsAsync(CancellationToken cancellationToken)
    {
        var meals = await _foodService.GetFavouriteMealsAsync(take: 10, cancellationToken);

        if (meals.Count == 0)
        {
            return Result("food.weekly.favourites", "No meal history yet. Log your meals to build your favourites list.");
        }

        var sb = new StringBuilder();
        sb.AppendLine($"⭐ Your top meals ({meals.Count}):");
        sb.AppendLine();

        for (var i = 0; i < meals.Count; i++)
        {
            var meal = meals[i];
            var last = meal.LastEatenAt.HasValue ? $" — last: {meal.LastEatenAt.Value:MMM d}" : string.Empty;
            sb.AppendLine($"{i + 1}. {meal.MealName} × {meal.TimesEaten}{last}");
        }

        return Result("food.weekly.favourites", sb.ToString().TrimEnd());
    }

    internal async Task<ConversationAgentResult> HandleLogMealPromptAsync(CancellationToken cancellationToken)
    {
        var meals = await _foodService.GetAllMealsAsync(cancellationToken);

        if (meals.Count == 0)
        {
            return Result("food.weekly.log.prompt", "No meals found. Add meals in Notion Meal Plans first, then sync.");
        }

        var sb = new StringBuilder();
        sb.AppendLine("Which meal did you eat? Reply with the meal ID and optional servings:");
        sb.AppendLine("  Format: <id>  or  <id> <servings>  (e.g. \"5\" or \"5 2.5\")");
        sb.AppendLine();

        foreach (var meal in meals)
        {
            var calories = meal.CaloriesPerServing.HasValue ? $" {meal.CaloriesPerServing} kcal" : string.Empty;
            sb.AppendLine($"  [{meal.Id}] {meal.Name}{calories} (default {meal.DefaultServings} serving(s))");
        }

        return Result("food.weekly.log.prompt", sb.ToString().TrimEnd());
    }

    internal static ConversationAgentResult HandleMealCreatePrompt()
    {
        return Result(
            "food.weekly.create.prompt",
            "What meal would you like to create? Type the name (e.g. \"Chicken Curry\" or \"Pasta Carbonara\").");
    }

    internal async Task<ConversationAgentResult> HandleDailyGoalAsync(CancellationToken cancellationToken)
    {
        const int defaultGoal = 2000;
        var progress = await _foodService.GetDailyProgressAsync(defaultGoal, cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine($"Daily progress \u2014 {DateTime.UtcNow:MMM d}");
        sb.AppendLine();
        var bar = BuildProgressBar(progress.PercentComplete);
        sb.AppendLine($"{bar} {progress.PercentComplete:F0}%");
        sb.AppendLine($"Consumed: {progress.ConsumedCalories} / {progress.GoalCalories} kcal");
        sb.AppendLine($"Remaining: {progress.RemainingCalories} kcal");
        sb.AppendLine($"Meals logged: {progress.MealsLogged}");

        return Result("food.weekly.goal", sb.ToString().TrimEnd());
    }

    internal async Task<ConversationAgentResult> HandleDietDiversityAsync(CancellationToken cancellationToken)
    {
        var div = await _foodService.GetDietDiversityAsync(7, cancellationToken);

        if (div.TotalMeals == 0)
            return Result("food.weekly.diversity", "No meals logged in the past 7 days.");

        var sb = new StringBuilder();
        sb.AppendLine($"Diet diversity \u2014 last {div.DaysAnalyzed} days");
        sb.AppendLine();
        sb.AppendLine($"Total meals: {div.TotalMeals}");
        sb.AppendLine($"Unique meals: {div.UniqueMeals}");

        if (div.RepeatedMeals.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Most repeated:");
            foreach (var name in div.RepeatedMeals.Take(5))
                sb.AppendLine($"  \U0001f501 {name}");
        }

        var ratio = div.TotalMeals > 0 ? (decimal)div.UniqueMeals / div.TotalMeals * 100 : 0;
        sb.AppendLine();
        sb.AppendLine($"Diversity score: {ratio:F0}% unique");

        return Result("food.weekly.diversity", sb.ToString().TrimEnd());
    }

    private static string BuildProgressBar(decimal percent)
    {
        var filled = (int)(percent / 10);
        var empty = 10 - filled;
        return new string('\u2588', Math.Min(filled, 10)) + new string('\u2591', Math.Max(empty, 0));
    }

    internal async Task<ConversationAgentResult> AddItemFromTextAsync(string input, CancellationToken cancellationToken)
    {
        // Simple parsing: "Milk 2L SuperMart" → name=Milk, qty=2L, store=SuperMart
        var parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var name = parts[0];
        var quantity = parts.Length >= 2 ? parts[1] : null;
        var store = parts.Length >= 3 ? string.Join(" ", parts.Skip(2)) : null;

        var item = await _foodService.AddGroceryItemAsync(name, quantity, store, cancellationToken);

        var qty = item.Quantity is not null ? $" × {item.Quantity}" : string.Empty;
        var st = item.Store is not null ? $" at {item.Store}" : string.Empty;

        return Result("food.shop.added", $"Added \"{item.Name}\"{qty}{st} to your shopping list.");
    }

    private static ConversationAgentResult Result(string intent, string message)
        => ConversationAgentResult.Empty("food-tracking-agent", intent, message);
}
