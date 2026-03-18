using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.Interfaces;

namespace LagerthaAssistant.Infrastructure.Services;

public sealed class LocalizationService : ILocalizationService
{
    private static readonly IReadOnlyDictionary<string, string> English = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["menu.main.title"] = "What can I help you with?",
        ["menu.main.chat"] = "🗣 Chat",
        ["menu.main.vocabulary"] = "📚 Vocabulary",
        ["menu.main.shopping"] = "🛒 Shopping",
        ["menu.main.menu"] = "🍽 Menu",
        ["menu.vocabulary.title"] = "📚 *Vocabulary*\n\nYou have *{0} words*. What shall we do?",
        ["menu.vocabulary.add"] = "➕ Add word",
        ["menu.vocabulary.list"] = "📋 My list",
        ["menu.vocabulary.url"] = "🔗 From URL",
        ["menu.vocabulary.batch"] = "📦 Batch mode",
        ["menu.vocabulary.back"] = "🔙 Main menu",
        ["menu.shopping.title"] = "🛒 *Shopping*",
        ["menu.shopping.add"] = "➕ Add item",
        ["menu.shopping.list"] = "📋 View list",
        ["menu.shopping.delete"] = "🗑 Remove item",
        ["menu.shopping.back"] = "🔙 Main menu",
        ["menu.weekly.title"] = "🍽 *Weekly Menu*",
        ["menu.weekly.view"] = "📅 View menu",
        ["menu.weekly.plan"] = "🤖 Plan with AI",
        ["menu.weekly.calories"] = "📊 Calories",
        ["menu.weekly.back"] = "🔙 Main menu",
        ["stub.wip"] = "🚧 This feature is under development. Coming soon!",
        ["start.welcome"] = "👋 Hi! I'm Lagertha — your personal assistant.\n\nI can help you:\n📚 Learn English words\n🛒 Keep a shopping list\n🍽 Plan your weekly menu\n🗣 Chat and help with anything\n\nChoose a section or just write to me!",
        ["locale.switched"] = "🌐 Language set to English",
        ["vocab.add.prompt"] = "Send me a word or phrase in English and I'll look it up for you.",
        ["vocab.url.prompt"] = "Send me a URL to an article and I'll extract new words for you.",
        ["vocab.list.empty"] = "Your vocabulary list is empty.",
        ["vocab.list.title"] = "📋 *Your latest words:*"
    };

    private static readonly IReadOnlyDictionary<string, string> Ukrainian = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["menu.main.title"] = "Чим можу допомогти?",
        ["menu.main.chat"] = "🗣 Чат",
        ["menu.main.vocabulary"] = "📚 Словник",
        ["menu.main.shopping"] = "🛒 Покупки",
        ["menu.main.menu"] = "🍽 Меню",
        ["menu.vocabulary.title"] = "📚 *Словник*\n\nУ тебе *{0} слів*. Що зробимо?",
        ["menu.vocabulary.add"] = "➕ Додати слово",
        ["menu.vocabulary.list"] = "📋 Мій список",
        ["menu.vocabulary.url"] = "🔗 З посилання",
        ["menu.vocabulary.batch"] = "📦 Batch режим",
        ["menu.vocabulary.back"] = "🔙 Головне меню",
        ["menu.shopping.title"] = "🛒 *Покупки*",
        ["menu.shopping.add"] = "➕ Додати товар",
        ["menu.shopping.list"] = "📋 Список",
        ["menu.shopping.delete"] = "🗑 Видалити товар",
        ["menu.shopping.back"] = "🔙 Головне меню",
        ["menu.weekly.title"] = "🍽 *Меню на тиждень*",
        ["menu.weekly.view"] = "📅 Переглянути меню",
        ["menu.weekly.plan"] = "🤖 Запропонуй меню",
        ["menu.weekly.calories"] = "📊 Калорії",
        ["menu.weekly.back"] = "🔙 Головне меню",
        ["stub.wip"] = "🚧 Ця функція в розробці. Скоро буде!",
        ["start.welcome"] = "👋 Привіт! Я Lagertha — твій особистий асистент.\n\nЯ вмію:\n📚 Вивчати англійські слова\n🛒 Вести список покупок\n🍽 Планувати меню на тиждень\n🗣 Просто розмовляти та допомагати\n\nОбери розділ або просто напиши мені!",
        ["locale.switched"] = "🌐 Мову змінено на українську",
        ["vocab.add.prompt"] = "Надішли слово або фразу англійською, і я знайду її для тебе.",
        ["vocab.url.prompt"] = "Надішли посилання на статтю, і я витягну нові слова.",
        ["vocab.list.empty"] = "Твій список слів поки порожній.",
        ["vocab.list.title"] = "📋 *Останні слова:*"
    };

    public string Get(string key, string locale)
    {
        var normalizedLocale = NormalizeLocale(locale);
        var dictionary = string.Equals(normalizedLocale, LocalizationConstants.UkrainianLocale, StringComparison.Ordinal)
            ? Ukrainian
            : English;

        if (dictionary.TryGetValue(key, out var value))
        {
            return value;
        }

        return English.TryGetValue(key, out value)
            ? value
            : key;
    }

    public string GetLocaleForUser(string? telegramLanguageCode)
    {
        if (IsRussian(telegramLanguageCode))
        {
            return LocalizationConstants.UkrainianLocale;
        }

        if (telegramLanguageCode?.StartsWith(LocalizationConstants.UkrainianLocale, StringComparison.OrdinalIgnoreCase) == true)
        {
            return LocalizationConstants.UkrainianLocale;
        }

        return LocalizationConstants.EnglishLocale;
    }

    public bool IsRussian(string? languageCode)
        => languageCode?.StartsWith("ru", StringComparison.OrdinalIgnoreCase) == true;

    private static string NormalizeLocale(string? locale)
        => string.Equals(locale, LocalizationConstants.UkrainianLocale, StringComparison.OrdinalIgnoreCase)
            ? LocalizationConstants.UkrainianLocale
            : LocalizationConstants.EnglishLocale;
}
