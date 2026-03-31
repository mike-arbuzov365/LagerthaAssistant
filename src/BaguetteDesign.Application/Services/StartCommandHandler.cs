namespace BaguetteDesign.Application.Services;

using BaguetteDesign.Application.Interfaces;
using BaguetteDesign.Domain.Enums;
using BaguetteDesign.Domain.Interfaces;

public sealed class StartCommandHandler : IStartCommandHandler
{
    private readonly IRoleRouter _roleRouter;
    private readonly ITelegramBotSender _sender;

    public StartCommandHandler(IRoleRouter roleRouter, ITelegramBotSender sender)
    {
        _roleRouter = roleRouter;
        _sender = sender;
    }

    public async Task HandleAsync(
        long chatId,
        long userId,
        string? languageCode,
        CancellationToken cancellationToken = default)
    {
        var role = _roleRouter.Resolve(userId);
        var locale = ResolveLocale(languageCode);

        var (text, keyboard) = role switch
        {
            UserRole.Designer => BuildDesignerMenu(locale),
            _ => BuildClientMenu(locale)
        };

        await _sender.SendTextAsync(
            chatId,
            text,
            new TelegramSendOptions(ParseMode: "HTML", ReplyMarkup: keyboard),
            cancellationToken: cancellationToken);
    }

    private static (string text, object keyboard) BuildClientMenu(string locale)
    {
        var text = locale == "uk"
            ? "Вітаю! Я допоможу вам замовити дизайн."
            : "Welcome! I'll help you order a design.";

        var keyboard = new
        {
            inline_keyboard = new[]
            {
                new[] { Btn("📋 Бриф", "brief"), Btn("💰 Прайс", "price") },
                new[] { Btn("🎨 Портфоліо", "portfolio"), Btn("💬 Питання", "question") },
                new[] { Btn("🔗 Зв'язатися", "contact"), Btn("📊 Статус", "status") }
            }
        };

        return (text, keyboard);
    }

    private static (string text, object keyboard) BuildDesignerMenu(string locale)
    {
        var text = locale == "uk"
            ? "Привіт! Твоя робоча панель:"
            : "Hello! Your workspace:";

        var keyboard = new
        {
            inline_keyboard = new[]
            {
                new[] { Btn("📩 Inbox", "inbox"), Btn("👤 Ліди", "leads") },
                new[] { Btn("📁 Проєкти", "projects"), Btn("⚡ Швидка дія", "quick") },
                new[] { Btn("⚙️ Налаштування", "settings") }
            }
        };

        return (text, keyboard);
    }

    private static object Btn(string text, string callbackData)
        => new { text, callback_data = callbackData };

    private static string ResolveLocale(string? languageCode)
        => languageCode?.StartsWith("uk", StringComparison.OrdinalIgnoreCase) == true ? "uk" : "en";
}
