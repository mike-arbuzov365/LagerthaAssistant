namespace BaguetteDesign.Application.Services;

using BaguetteDesign.Application.Interfaces;
using BaguetteDesign.Domain.Entities;

public sealed class PriceHandler : IPriceHandler
{
    private readonly IPriceService _priceService;
    private readonly ITelegramBotSender _sender;

    public PriceHandler(IPriceService priceService, ITelegramBotSender sender)
    {
        _priceService = priceService;
        _sender = sender;
    }

    public async Task ShowCategoriesAsync(long chatId, string? languageCode, CancellationToken cancellationToken = default)
    {
        var locale = ResolveLocale(languageCode);
        var categories = await _priceService.GetCategoriesAsync(cancellationToken);

        if (categories.Count == 0)
        {
            var noData = locale == "uk"
                ? "💰 Прайс поки недоступний. Спробуйте пізніше або зв'яжіться з дизайнером."
                : "💰 Pricing is not available yet. Try later or contact the designer.";
            await _sender.SendTextAsync(chatId, noData, cancellationToken: cancellationToken);
            return;
        }

        var title = locale == "uk" ? "💰 <b>Оберіть категорію послуг:</b>" : "💰 <b>Select a service category:</b>";

        var categoryButtons = categories
            .Select(c => new[] { Btn(c, $"price_cat_{Encode(c)}") })
            .ToArray();

        var briefRow = new[] { Btn(locale == "uk" ? "📋 Перейти до брифу" : "📋 Go to brief", "brief") };
        var allRows = categoryButtons.Append(briefRow).ToArray();

        await _sender.SendTextAsync(
            chatId, title,
            new TelegramSendOptions(ParseMode: "HTML", ReplyMarkup: new { inline_keyboard = allRows }),
            cancellationToken: cancellationToken);
    }

    public async Task ShowCategoryItemsAsync(long chatId, string category, string? languageCode, CancellationToken cancellationToken = default)
    {
        var locale = ResolveLocale(languageCode);
        var items = await _priceService.GetByCategoryAsync(category, cancellationToken);

        if (items.Count == 0)
        {
            var noData = locale == "uk"
                ? $"У категорії <b>{category}</b> поки немає позицій."
                : $"No items in category <b>{category}</b> yet.";
            await _sender.SendTextAsync(chatId, noData,
                new TelegramSendOptions(ParseMode: "HTML"),
                cancellationToken: cancellationToken);
            return;
        }

        var text = BuildCategoryText(category, items, locale);

        var keyboard = new
        {
            inline_keyboard = new[]
            {
                new[] { Btn(locale == "uk" ? "◀️ Назад до категорій" : "◀️ Back to categories", "price") },
                new[] { Btn(locale == "uk" ? "📋 Замовити бриф" : "📋 Order brief", "brief") }
            }
        };

        await _sender.SendTextAsync(
            chatId, text,
            new TelegramSendOptions(ParseMode: "HTML", ReplyMarkup: keyboard),
            cancellationToken: cancellationToken);
    }

    private static string BuildCategoryText(string category, IReadOnlyList<PriceItem> items, string locale)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"💰 <b>{category}</b>\n");

        foreach (var item in items)
        {
            sb.Append($"• <b>{item.Name}</b>");
            if (item.PriceAmount.HasValue)
                sb.Append($" — {item.PriceAmount:0.##} {item.Currency}");
            if (!string.IsNullOrWhiteSpace(item.Description))
                sb.AppendLine().Append($"  <i>{item.Description}</i>");
            sb.AppendLine();
        }

        if (locale == "uk")
            sb.AppendLine("\nЦіни орієнтовні. Фінальна вартість обговорюється в брифі.");
        else
            sb.AppendLine("\nPrices are approximate. Final cost is discussed in the brief.");

        return sb.ToString().TrimEnd();
    }

    private static object Btn(string text, string callbackData)
        => new { text, callback_data = callbackData };

    // Encode category name for safe use in callback_data (max 64 chars)
    private static string Encode(string value)
        => Uri.EscapeDataString(value)[..Math.Min(40, Uri.EscapeDataString(value).Length)];

    private static string ResolveLocale(string? languageCode)
        => languageCode?.StartsWith("uk", StringComparison.OrdinalIgnoreCase) == true ? "uk" : "en";
}
