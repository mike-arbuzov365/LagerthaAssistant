namespace BaguetteDesign.Application.Services;

using BaguetteDesign.Application.Interfaces;
using BaguetteDesign.Domain.Entities;

public sealed class PortfolioHandler : IPortfolioHandler
{
    private readonly IPortfolioService _portfolioService;
    private readonly ITelegramBotSender _sender;

    public PortfolioHandler(IPortfolioService portfolioService, ITelegramBotSender sender)
    {
        _portfolioService = portfolioService;
        _sender = sender;
    }

    public async Task ShowCategoriesAsync(long chatId, string? languageCode, CancellationToken cancellationToken = default)
    {
        var locale = ResolveLocale(languageCode);
        var categories = await _portfolioService.GetCategoriesAsync(cancellationToken);

        if (categories.Count == 0)
        {
            var noData = locale == "uk"
                ? "🎨 Портфоліо поки недоступне. Спробуйте пізніше або зв'яжіться з дизайнером."
                : "🎨 Portfolio is not available yet. Try later or contact the designer.";
            await _sender.SendTextAsync(chatId, noData, cancellationToken: cancellationToken);
            return;
        }

        var title = locale == "uk"
            ? "🎨 <b>Оберіть категорію робіт:</b>"
            : "🎨 <b>Select a work category:</b>";

        var categoryButtons = categories
            .Select(c => new[] { Btn(c, $"portfolio_cat_{Encode(c)}") })
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
        var cases = await _portfolioService.GetByCategoryAsync(category, cancellationToken);

        if (cases.Count == 0)
        {
            var noData = locale == "uk"
                ? $"У категорії <b>{category}</b> поки немає кейсів."
                : $"No cases in category <b>{category}</b> yet.";
            await _sender.SendTextAsync(chatId, noData,
                new TelegramSendOptions(ParseMode: "HTML"),
                cancellationToken: cancellationToken);
            return;
        }

        // Send each case as a separate message (image + caption or text only)
        foreach (var portfolioCase in cases)
        {
            await SendCaseAsync(chatId, portfolioCase, locale, cancellationToken);
        }

        // Final navigation message
        var navText = locale == "uk"
            ? "Що бажаєте зробити далі?"
            : "What would you like to do next?";

        var navKeyboard = new
        {
            inline_keyboard = new[]
            {
                new[] { Btn(locale == "uk" ? "◀️ Назад до категорій" : "◀️ Back to categories", "portfolio") },
                new[] { Btn(locale == "uk" ? "📋 Замовити бриф" : "📋 Order brief", "brief") }
            }
        };

        await _sender.SendTextAsync(
            chatId, navText,
            new TelegramSendOptions(ParseMode: "HTML", ReplyMarkup: navKeyboard),
            cancellationToken: cancellationToken);
    }

    private async Task SendCaseAsync(long chatId, PortfolioCase portfolioCase, string locale, CancellationToken ct)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"<b>{portfolioCase.Title}</b>");

        if (!string.IsNullOrWhiteSpace(portfolioCase.Description))
            sb.AppendLine(portfolioCase.Description);

        if (!string.IsNullOrWhiteSpace(portfolioCase.Tags))
            sb.AppendLine($"\n<i>{portfolioCase.Tags}</i>");

        var wantSimilarLabel = locale == "uk" ? "🎯 Хочу схожий" : "🎯 Want similar";
        var keyboard = new
        {
            inline_keyboard = new[]
            {
                new[] { Btn(wantSimilarLabel, $"portfolio_similar_{Encode(portfolioCase.Title)}") }
            }
        };

        await _sender.SendTextAsync(
            chatId, sb.ToString().TrimEnd(),
            new TelegramSendOptions(ParseMode: "HTML", ReplyMarkup: keyboard),
            cancellationToken: ct);
    }

    private static object Btn(string text, string callbackData)
        => new { text, callback_data = callbackData };

    private static string Encode(string value)
    {
        var encoded = Uri.EscapeDataString(value);
        return encoded[..Math.Min(40, encoded.Length)];
    }

    private static string ResolveLocale(string? languageCode)
        => languageCode?.StartsWith("uk", StringComparison.OrdinalIgnoreCase) == true ? "uk" : "en";
}
