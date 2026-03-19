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
        ["menu.main.settings"] = "⚙️ Settings",
        ["menu.vocabulary.title"] = "📚 Vocabulary\n\nYou have {0} words. What shall we do?",
        ["menu.vocabulary.add"] = "➕ Add word",
        ["menu.vocabulary.list"] = "📋 My list",
        ["menu.vocabulary.url"] = "🔗 From URL",
        ["menu.vocabulary.batch"] = "📦 Batch mode",
        ["menu.vocabulary.back"] = "🔙 Main menu",
        ["menu.shopping.title"] = "🛒 Shopping",
        ["menu.shopping.add"] = "➕ Add item",
        ["menu.shopping.list"] = "📋 View list",
        ["menu.shopping.delete"] = "🗑 Remove item",
        ["menu.shopping.back"] = "🔙 Main menu",
        ["menu.weekly.title"] = "🍽 Weekly Menu",
        ["menu.weekly.view"] = "📅 View menu",
        ["menu.weekly.plan"] = "🤖 Plan with AI",
        ["menu.weekly.calories"] = "📊 Calories",
        ["menu.weekly.back"] = "🔙 Main menu",
        ["stub.wip"] = "🚧 This feature is under development. Coming soon!",
        ["start.welcome"] = "👋 Hi! I'm Lagertha — your personal assistant.\n\nI can help you:\n📚 Learn English words\n🛒 Keep a shopping list\n🍽 Plan your weekly menu\n🗣 Chat and help with anything\n\nChoose a section or just write to me!",
        ["locale.switched"] = "🌐 Language set to English",
        ["vocab.add.prompt"] = "Send me a word or phrase in English and I'll look it up for you.",
        ["vocab.url.prompt"] = "Send me a URL to an article and I'll extract new words for you.",
        ["vocab.batch.prompt"] = "Send several words or phrases in one message (new lines, commas, semicolons, or sentences), and I will process them in batch mode.",
        ["vocab.list.empty"] = "Your vocabulary list is empty.",
        ["vocab.list.title"] = "📋 Your latest words:",
        ["onboarding.choose_language"] = "👋 Welcome!\n\nPlease choose your language:",
        ["onboarding.language_saved"] = "🎉 Great! Language saved.\n\nHi! I'm Lagertha — your personal assistant. How can I help?",
        ["settings.title"] = "⚙️ <b>Settings</b>",
        ["settings.language"] = "🌐 Language",
        ["settings.save_mode"] = "💾 Save mode",
        ["settings.onedrive"] = "☁️ OneDrive / Graph",
        ["settings.notion"] = "📝 Notion (coming soon)",
        ["settings.back"] = "🔙 Main menu",
        ["settings.change_language"] = "🌐 Change language",
        ["settings.change_save_mode"] = "💾 Change save mode",
        ["language.current"] = "🌐 <b>Language</b>\n\nCurrent: {0}\n\nChoose new language:",
        ["language.changed"] = "✅ Language changed to {0}",
        ["savemode.title"] = "💾 <b>Save mode</b>\n\nCurrent: <b>{0}</b>\n\n• <b>auto</b> — save words automatically\n• <b>ask</b> — confirm before each save\n• <b>off</b> — don't save to deck",
        ["savemode.changed"] = "✅ Save mode changed to <b>{0}</b>",
        ["savemode.auto"] = "💾 auto",
        ["savemode.ask"] = "❓ ask",
        ["savemode.off"] = "🚫 off",
        ["onedrive.title"] = "☁️ <b>OneDrive / Graph</b>",
        ["onedrive.status_connected"] = "Status: ✅ Connected",
        ["onedrive.status_disconnected"] = "Status: ❌ Not connected",
        ["onedrive.login"] = "🔑 Sign in to OneDrive",
        ["onedrive.logout"] = "🚪 Sign out",
        ["onedrive.login_started"] = "🔑 To sign in, open this link and enter the code:\n\n<b>{0}</b>\n\nLink: {1}\n\nCode expires in {2} minutes.",
        ["onedrive.logout_done"] = "✅ Signed out from OneDrive.",
        ["onedrive.check_status"] = "✅ I've signed in",
        ["onedrive.still_not_signed_in"] = "Still not signed in. Try again?",
        ["notion.title"] = "📝 <b>Notion</b>\n\n🚧 This integration is under development. Coming soon!",
        ["back"] = "🔙 Back",
        ["language.name.uk"] = "🇺🇦 Українська",
        ["language.name.en"] = "🇬🇧 English",
        ["language.name.es"] = "🇪🇸 Español",
        ["language.name.fr"] = "🇫🇷 Français",
        ["language.name.de"] = "🇩🇪 Deutsch",
        ["language.name.pl"] = "🇵🇱 Polski",
        ["language.name.de_pl"] = "🇩🇪 Deutsch · 🇵🇱 Polski"
    };

    private static readonly IReadOnlyDictionary<string, string> Ukrainian = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["menu.main.title"] = "Чим можу допомогти?",
        ["menu.main.chat"] = "🗣 Чат",
        ["menu.main.vocabulary"] = "📚 Словник",
        ["menu.main.shopping"] = "🛒 Покупки",
        ["menu.main.menu"] = "🍽 Меню",
        ["menu.main.settings"] = "⚙️ Налаштування",
        ["menu.vocabulary.title"] = "📚 Словник\n\nУ тебе {0} слів. Що зробимо?",
        ["menu.vocabulary.add"] = "➕ Додати слово",
        ["menu.vocabulary.list"] = "📋 Мій список",
        ["menu.vocabulary.url"] = "🔗 З посилання",
        ["menu.vocabulary.batch"] = "📦 Batch режим",
        ["menu.vocabulary.back"] = "🔙 Головне меню",
        ["menu.shopping.title"] = "🛒 Покупки",
        ["menu.shopping.add"] = "➕ Додати товар",
        ["menu.shopping.list"] = "📋 Список",
        ["menu.shopping.delete"] = "🗑 Видалити товар",
        ["menu.shopping.back"] = "🔙 Головне меню",
        ["menu.weekly.title"] = "🍽 Меню на тиждень",
        ["menu.weekly.view"] = "📅 Переглянути меню",
        ["menu.weekly.plan"] = "🤖 Запропонуй меню",
        ["menu.weekly.calories"] = "📊 Калорії",
        ["menu.weekly.back"] = "🔙 Головне меню",
        ["stub.wip"] = "🚧 Ця функція в розробці. Скоро буде!",
        ["start.welcome"] = "👋 Привіт! Я Lagertha — твій особистий асистент.\n\nЯ вмію:\n📚 Вивчати англійські слова\n🛒 Вести список покупок\n🍽 Планувати меню на тиждень\n🗣 Просто розмовляти та допомагати\n\nОбери розділ або просто напиши мені!",
        ["locale.switched"] = "🌐 Мову змінено на українську",
        ["vocab.add.prompt"] = "Надішли слово або фразу англійською, і я знайду її для тебе.",
        ["vocab.url.prompt"] = "Надішли посилання на статтю, і я витягну нові слова.",
        ["vocab.batch.prompt"] = "Надішли кілька слів або фраз одним повідомленням (новими рядками, комами, крапкою з комою або реченнями), і я оброблю їх у batch режимі.",
        ["vocab.list.empty"] = "Твій список слів поки порожній.",
        ["vocab.list.title"] = "📋 Останні слова:",
        ["onboarding.choose_language"] = "👋 Вітаємо!\n\nБудь ласка, оберіть мову:",
        ["onboarding.language_saved"] = "🎉 Чудово! Мову збережено.\n\nПривіт! Я Lagertha — твій особистий асистент. Чим можу допомогти?",
        ["settings.title"] = "⚙️ <b>Налаштування</b>",
        ["settings.language"] = "🌐 Мова",
        ["settings.save_mode"] = "💾 Режим збереження",
        ["settings.onedrive"] = "☁️ OneDrive / Graph",
        ["settings.notion"] = "📝 Notion (скоро)",
        ["settings.back"] = "🔙 Головне меню",
        ["settings.change_language"] = "🌐 Змінити мову",
        ["settings.change_save_mode"] = "💾 Змінити режим збереження",
        ["language.current"] = "🌐 <b>Мова</b>\n\nПоточна: {0}\n\nОберіть нову мову:",
        ["language.changed"] = "✅ Мову змінено на {0}",
        ["savemode.title"] = "💾 <b>Режим збереження</b>\n\nПоточний: <b>{0}</b>\n\n• <b>auto</b> — зберігати автоматично\n• <b>ask</b> — підтверджувати перед збереженням\n• <b>off</b> — не зберігати в деку",
        ["savemode.changed"] = "✅ Режим збереження змінено на <b>{0}</b>",
        ["savemode.auto"] = "💾 auto",
        ["savemode.ask"] = "❓ ask",
        ["savemode.off"] = "🚫 off",
        ["onedrive.title"] = "☁️ <b>OneDrive / Graph</b>",
        ["onedrive.status_connected"] = "Статус: ✅ Підключено",
        ["onedrive.status_disconnected"] = "Статус: ❌ Не підключено",
        ["onedrive.login"] = "🔑 Увійти в OneDrive",
        ["onedrive.logout"] = "🚪 Вийти",
        ["onedrive.login_started"] = "🔑 Щоб увійти, відкрий посилання та введи код:\n\n<b>{0}</b>\n\nПосилання: {1}\n\nКод діє {2} хвилин.",
        ["onedrive.logout_done"] = "✅ Вихід з OneDrive виконано.",
        ["onedrive.check_status"] = "✅ Я вже увійшов",
        ["onedrive.still_not_signed_in"] = "Ще не авторизовано. Спробувати ще раз?",
        ["notion.title"] = "📝 <b>Notion</b>\n\n🚧 Ця інтеграція в розробці. Скоро буде!",
        ["back"] = "🔙 Назад",
        ["language.name.uk"] = "🇺🇦 Українська",
        ["language.name.en"] = "🇬🇧 English",
        ["language.name.es"] = "🇪🇸 Español",
        ["language.name.fr"] = "🇫🇷 Français",
        ["language.name.de"] = "🇩🇪 Deutsch",
        ["language.name.pl"] = "🇵🇱 Polski",
        ["language.name.de_pl"] = "🇩🇪 Deutsch · 🇵🇱 Polski"
    };

    // TODO: Replace English placeholders with native translations for Spanish.
    private static readonly IReadOnlyDictionary<string, string> Spanish = English;

    // TODO: Replace English placeholders with native translations for French.
    private static readonly IReadOnlyDictionary<string, string> French = English;

    public string Get(string key, string locale)
    {
        var normalizedLocale = LocalizationConstants.NormalizeLocaleCode(locale);
        var effectiveLocale = normalizedLocale switch
        {
            LocalizationConstants.GermanLocale => LocalizationConstants.EnglishLocale,
            LocalizationConstants.PolishLocale => LocalizationConstants.EnglishLocale,
            _ => normalizedLocale
        };

        var dictionary = effectiveLocale switch
        {
            LocalizationConstants.UkrainianLocale => Ukrainian,
            LocalizationConstants.SpanishLocale => Spanish,
            LocalizationConstants.FrenchLocale => French,
            _ => English
        };

        if (dictionary.TryGetValue(key, out var value))
        {
            return value;
        }

        if (English.TryGetValue(key, out var fallback))
        {
            return fallback;
        }

        return string.Empty;
    }

    public string GetLocaleForUser(string? telegramLanguageCode)
        => LocalizationConstants.NormalizeLocaleCode(telegramLanguageCode);

    public bool IsRussian(string? languageCode)
        => languageCode?.StartsWith("ru", StringComparison.OrdinalIgnoreCase) == true;
}
