namespace LagerthaAssistant.Application.Services.Agents;

using System.Text;
using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.Interfaces;
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
    private readonly ILocalizationService _loc;

    public FoodTrackingConversationAgent(IFoodTrackingService foodService, ILocalizationService loc)
    {
        _foodService = foodService;
        _loc = loc;
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
        var locale = context.Locale;

        return input switch
        {
            CallbackDataConstants.Shop.List => await HandleShopListAsync(locale, cancellationToken),
            CallbackDataConstants.Shop.Add => HandleShopAddPrompt(locale),
            CallbackDataConstants.Shop.Delete => await HandleShopClearAsync(locale, cancellationToken),
            CallbackDataConstants.Weekly.View => await HandleWeeklyViewAsync(locale, cancellationToken),
            CallbackDataConstants.Weekly.Plan => await HandleCookableNowAsync(locale, cancellationToken),
            CallbackDataConstants.Weekly.Calories => await HandleWeeklyCaloriesAsync(locale, cancellationToken),
            CallbackDataConstants.Weekly.Favourites => await HandleFavouriteMealsAsync(locale, cancellationToken),
            CallbackDataConstants.Weekly.Log => await HandleLogMealPromptAsync(locale, cancellationToken),
            CallbackDataConstants.Weekly.Create => HandleMealCreatePrompt(locale),
            CallbackDataConstants.Weekly.DailyGoal => await HandleDailyGoalAsync(locale, cancellationToken),
            CallbackDataConstants.Weekly.Diversity => await HandleDietDiversityAsync(locale, cancellationToken),
            _ => Result("food.unknown", _loc.Get("food.unknown", locale))
        };
    }

    // ── Handlers ─────────────────────────────────────────────────────────────

    internal async Task<ConversationAgentResult> HandleShopListAsync(string locale, CancellationToken cancellationToken)
    {
        var items = await _foodService.GetActiveGroceryListAsync(cancellationToken);

        if (items.Count == 0)
        {
            return Result("food.shop.list.empty", _loc.Get("food.shop.list.empty", locale));
        }

        var sb = new StringBuilder();
        sb.AppendLine(string.Format(_loc.Get("food.shop.list.title", locale), items.Count));
        sb.AppendLine();

        var noStore = _loc.Get("food.shop.list.no_store", locale);
        var byStore = items
            .GroupBy(x => x.Store ?? noStore)
            .OrderBy(g => g.Key);

        foreach (var group in byStore)
        {
            sb.AppendLine(string.Format(_loc.Get("food.shop.list.store", locale), group.Key));
            foreach (var item in group)
            {
                var qty = item.Quantity is not null ? $" — {item.Quantity}" : string.Empty;
                var cost = item.EstimatedCost.HasValue ? $" (~${item.EstimatedCost:F2})" : string.Empty;
                sb.AppendLine(string.Format(_loc.Get("food.shop.list.item", locale), item.Name, qty, cost));
            }

            sb.AppendLine();
        }

        return Result("food.shop.list", sb.ToString().TrimEnd());
    }

    internal ConversationAgentResult HandleShopAddPrompt(string locale)
    {
        return Result("food.shop.add.prompt", _loc.Get("food.shop.add.prompt", locale));
    }

    internal async Task<ConversationAgentResult> HandleShopClearAsync(string locale, CancellationToken cancellationToken)
    {
        var deleted = await _foodService.ClearBoughtItemsAsync(cancellationToken);
        return deleted == 0
            ? Result("food.shop.clear.none", _loc.Get("food.shop.clear.none", locale))
            : Result("food.shop.clear.done", string.Format(_loc.Get("food.shop.clear.done", locale), deleted));
    }

    internal async Task<ConversationAgentResult> HandleWeeklyViewAsync(string locale, CancellationToken cancellationToken)
    {
        var meals = await _foodService.GetAllMealsAsync(cancellationToken);

        if (meals.Count == 0)
        {
            return Result("food.weekly.view.empty", _loc.Get("food.weekly.view.empty", locale));
        }

        var sb = new StringBuilder();
        sb.AppendLine(string.Format(_loc.Get("food.weekly.view.title", locale), meals.Count));
        sb.AppendLine();

        foreach (var meal in meals)
        {
            var calories = meal.CaloriesPerServing.HasValue ? $" — {meal.CaloriesPerServing} kcal/serving" : string.Empty;
            var prep = meal.PrepTimeMinutes.HasValue ? $" ({meal.PrepTimeMinutes} min)" : string.Empty;
            sb.AppendLine($"🍽 {meal.Name}{calories}{prep}");

            if (meal.Ingredients.Count > 0)
            {
                var ingredientNames = meal.Ingredients.Take(5).Select(i => i.Name);
                var more = meal.Ingredients.Count > 5 ? "..." : string.Empty;
                sb.AppendLine(string.Format(_loc.Get("food.weekly.view.ingredients", locale), string.Join(", ", ingredientNames), more));
            }
        }

        return Result("food.weekly.view", sb.ToString().TrimEnd());
    }

    internal async Task<ConversationAgentResult> HandleCookableNowAsync(string locale, CancellationToken cancellationToken)
    {
        var meals = await _foodService.GetCookableNowAsync(cancellationToken);

        if (meals.Count == 0)
        {
            return Result("food.weekly.cookable.empty", _loc.Get("food.weekly.cookable.empty", locale));
        }

        var sb = new StringBuilder();
        sb.AppendLine(string.Format(_loc.Get("food.weekly.cookable.title", locale), meals.Count));
        sb.AppendLine();

        foreach (var meal in meals)
        {
            var calories = meal.CaloriesPerServing.HasValue ? $" — {meal.CaloriesPerServing} kcal/serving" : string.Empty;
            sb.AppendLine($"✅ {meal.Name}{calories}");
        }

        return Result("food.weekly.cookable", sb.ToString().TrimEnd());
    }

    internal async Task<ConversationAgentResult> HandleWeeklyCaloriesAsync(string locale, CancellationToken cancellationToken)
    {
        var to = DateTime.UtcNow;
        var from = to.AddDays(-7).Date;
        var summary = await _foodService.GetCalorieSummaryAsync(from, to, cancellationToken);

        if (summary.TotalCalories == 0)
        {
            return Result("food.weekly.calories.empty", _loc.Get("food.weekly.calories.empty", locale));
        }

        var sb = new StringBuilder();
        sb.AppendLine(string.Format(_loc.Get("food.weekly.calories.title", locale), from.ToString("MMM d"), to.ToString("MMM d")));
        sb.AppendLine();
        sb.AppendLine(string.Format(_loc.Get("food.weekly.calories.total", locale), summary.TotalCalories));
        sb.AppendLine(string.Format(_loc.Get("food.weekly.calories.avg", locale), summary.AvgCaloriesPerDay));
        sb.AppendLine();
        sb.AppendLine(string.Format(_loc.Get("food.weekly.calories.protein", locale), summary.TotalProteinGrams));
        sb.AppendLine(string.Format(_loc.Get("food.weekly.calories.carbs", locale), summary.TotalCarbsGrams));
        sb.AppendLine(string.Format(_loc.Get("food.weekly.calories.fat", locale), summary.TotalFatGrams));

        return Result("food.weekly.calories", sb.ToString().TrimEnd());
    }

    internal async Task<ConversationAgentResult> HandleFavouriteMealsAsync(string locale, CancellationToken cancellationToken)
    {
        var meals = await _foodService.GetFavouriteMealsAsync(take: 10, cancellationToken);

        if (meals.Count == 0)
        {
            return Result("food.weekly.favourites.empty", _loc.Get("food.weekly.favourites.empty", locale));
        }

        var sb = new StringBuilder();
        sb.AppendLine(string.Format(_loc.Get("food.weekly.favourites.title", locale), meals.Count));
        sb.AppendLine();

        for (var i = 0; i < meals.Count; i++)
        {
            var meal = meals[i];
            var last = meal.LastEatenAt.HasValue ? $" — last: {meal.LastEatenAt.Value:MMM d}" : string.Empty;
            sb.AppendLine($"{i + 1}. {meal.MealName} × {meal.TimesEaten}{last}");
        }

        return Result("food.weekly.favourites", sb.ToString().TrimEnd());
    }

    internal async Task<ConversationAgentResult> HandleLogMealPromptAsync(string locale, CancellationToken cancellationToken)
    {
        var meals = await _foodService.GetAllMealsAsync(cancellationToken);

        if (meals.Count == 0)
        {
            return Result("food.weekly.log.empty", _loc.Get("food.weekly.log.empty", locale));
        }

        var sb = new StringBuilder();
        sb.AppendLine(_loc.Get("food.weekly.log.prompt", locale));
        sb.AppendLine();

        foreach (var meal in meals)
        {
            var calories = meal.CaloriesPerServing.HasValue ? $" {meal.CaloriesPerServing} kcal" : string.Empty;
            sb.AppendLine($"  [{meal.Id}] {meal.Name}{calories} (default {meal.DefaultServings} serving(s))");
        }

        return Result("food.weekly.log.prompt", sb.ToString().TrimEnd());
    }

    internal ConversationAgentResult HandleMealCreatePrompt(string locale)
    {
        return Result("food.weekly.create.prompt", _loc.Get("food.weekly.create.prompt", locale));
    }

    internal async Task<ConversationAgentResult> HandleDailyGoalAsync(string locale, CancellationToken cancellationToken)
    {
        const int defaultGoal = 2000;
        var progress = await _foodService.GetDailyProgressAsync(defaultGoal, cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine(string.Format(_loc.Get("food.weekly.goal.title", locale), DateTime.UtcNow.ToString("MMM d")));
        sb.AppendLine();
        var bar = BuildProgressBar(progress.PercentComplete);
        sb.AppendLine($"{bar} {progress.PercentComplete:F0}%");
        sb.AppendLine(string.Format(_loc.Get("food.weekly.goal.consumed", locale), progress.ConsumedCalories, progress.GoalCalories));
        sb.AppendLine(string.Format(_loc.Get("food.weekly.goal.remaining", locale), progress.RemainingCalories));
        sb.AppendLine(string.Format(_loc.Get("food.weekly.goal.meals", locale), progress.MealsLogged));

        return Result("food.weekly.goal", sb.ToString().TrimEnd());
    }

    internal async Task<ConversationAgentResult> HandleDietDiversityAsync(string locale, CancellationToken cancellationToken)
    {
        var div = await _foodService.GetDietDiversityAsync(7, cancellationToken);

        if (div.TotalMeals == 0)
        {
            return Result("food.weekly.diversity.empty", _loc.Get("food.weekly.diversity.empty", locale));
        }

        var sb = new StringBuilder();
        sb.AppendLine(string.Format(_loc.Get("food.weekly.diversity.title", locale), div.DaysAnalyzed));
        sb.AppendLine();
        sb.AppendLine(string.Format(_loc.Get("food.weekly.diversity.total", locale), div.TotalMeals));
        sb.AppendLine(string.Format(_loc.Get("food.weekly.diversity.unique", locale), div.UniqueMeals));

        if (div.RepeatedMeals.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine(_loc.Get("food.weekly.diversity.repeated", locale));
            foreach (var name in div.RepeatedMeals.Take(5))
                sb.AppendLine($"  \U0001f501 {name}");
        }

        var ratio = div.TotalMeals > 0 ? (decimal)div.UniqueMeals / div.TotalMeals * 100 : 0;
        sb.AppendLine();
        sb.AppendLine(string.Format(_loc.Get("food.weekly.diversity.score", locale), ratio));

        return Result("food.weekly.diversity", sb.ToString().TrimEnd());
    }

    private static string BuildProgressBar(decimal percent)
    {
        var filled = (int)(percent / 10);
        var empty = 10 - filled;
        return new string('\u2588', Math.Min(filled, 10)) + new string('\u2591', Math.Max(empty, 0));
    }

    internal async Task<ConversationAgentResult> AddItemFromTextAsync(string input, string locale, CancellationToken cancellationToken)
    {
        var parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var name = parts[0];
        var quantity = parts.Length >= 2 ? parts[1] : null;
        var store = parts.Length >= 3 ? string.Join(" ", parts.Skip(2)) : null;

        var item = await _foodService.AddGroceryItemAsync(name, quantity, store, cancellationToken);

        var qty = item.Quantity is not null ? $" × {item.Quantity}" : string.Empty;
        var st = item.Store is not null ? $" at {item.Store}" : string.Empty;

        return Result("food.shop.added", string.Format(_loc.Get("food.shop.added", locale), item.Name, qty, st));
    }

    private static ConversationAgentResult Result(string intent, string message)
        => ConversationAgentResult.Empty("food-tracking-agent", intent, message);
}
