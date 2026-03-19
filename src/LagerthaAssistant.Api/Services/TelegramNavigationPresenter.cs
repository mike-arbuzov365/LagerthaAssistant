using LagerthaAssistant.Api.Contracts;
using LagerthaAssistant.Api.Interfaces;
using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.Interfaces;
using LagerthaAssistant.Application.Navigation;

namespace LagerthaAssistant.Api.Services;

public sealed class TelegramNavigationPresenter : ITelegramNavigationPresenter
{
    private readonly ILocalizationService _localizationService;

    public TelegramNavigationPresenter(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
    }

    public MainMenuLabels GetMainMenuLabels(string locale)
    {
        return new MainMenuLabels(
            Chat: _localizationService.Get("menu.main.chat", locale),
            Vocabulary: _localizationService.Get("menu.main.vocabulary", locale),
            Shopping: _localizationService.Get("menu.main.shopping", locale),
            WeeklyMenu: _localizationService.Get("menu.main.menu", locale),
            Settings: _localizationService.Get("menu.main.settings", locale));
    }

    public string GetText(string key, string locale, params object[] args)
    {
        var template = _localizationService.Get(key, locale);
        return args.Length == 0
            ? template
            : string.Format(template, args);
    }

    public string GetLanguageDisplayName(string locale)
    {
        var normalized = LocalizationConstants.NormalizeLocaleCode(locale);
        return normalized switch
        {
            LocalizationConstants.UkrainianLocale => _localizationService.Get("language.name.uk", normalized),
            LocalizationConstants.SpanishLocale => _localizationService.Get("language.name.es", normalized),
            LocalizationConstants.FrenchLocale => _localizationService.Get("language.name.fr", normalized),
            LocalizationConstants.GermanLocale => _localizationService.Get("language.name.de", normalized),
            LocalizationConstants.PolishLocale => _localizationService.Get("language.name.pl", normalized),
            _ => _localizationService.Get("language.name.en", normalized)
        };
    }

    public TelegramReplyKeyboardMarkup BuildMainReplyKeyboard(string locale)
    {
        var labels = GetMainMenuLabels(locale);
        return new TelegramReplyKeyboardMarkup(
            Keyboard:
            [
                [new TelegramKeyboardButton(labels.Chat), new TelegramKeyboardButton(labels.Vocabulary)],
                [new TelegramKeyboardButton(labels.Shopping), new TelegramKeyboardButton(labels.WeeklyMenu)],
                [new TelegramKeyboardButton(labels.Settings)]
            ],
            ResizeKeyboard: true,
            IsPersistent: true);
    }

    public TelegramInlineKeyboardMarkup BuildVocabularyKeyboard(string locale)
    {
        return new TelegramInlineKeyboardMarkup(
            InlineKeyboard:
            [
                [Button("menu.vocabulary.add", locale, "vocab:add"), Button("menu.vocabulary.list", locale, "vocab:list")],
                [Button("menu.vocabulary.url", locale, "vocab:url"), Button("menu.vocabulary.batch", locale, "vocab:batch")],
                [Button("menu.vocabulary.back", locale, "nav:main")]
            ]);
    }

    public TelegramInlineKeyboardMarkup BuildShoppingKeyboard(string locale)
    {
        return new TelegramInlineKeyboardMarkup(
            InlineKeyboard:
            [
                [Button("menu.shopping.add", locale, "shop:add"), Button("menu.shopping.list", locale, "shop:list")],
                [Button("menu.shopping.delete", locale, "shop:delete")],
                [Button("menu.shopping.back", locale, "nav:main")]
            ]);
    }

    public TelegramInlineKeyboardMarkup BuildWeeklyMenuKeyboard(string locale)
    {
        return new TelegramInlineKeyboardMarkup(
            InlineKeyboard:
            [
                [Button("menu.weekly.view", locale, "weekly:view"), Button("menu.weekly.plan", locale, "weekly:plan")],
                [Button("menu.weekly.calories", locale, "weekly:calories")],
                [Button("menu.weekly.back", locale, "nav:main")]
            ]);
    }

    public TelegramInlineKeyboardMarkup BuildOnboardingLanguageKeyboard(string locale)
    {
        return new TelegramInlineKeyboardMarkup(
            InlineKeyboard:
            [
                [LanguageButton(locale, "language.name.uk", "lang:uk"), LanguageButton(locale, "language.name.en", "lang:en")],
                [LanguageButton(locale, "language.name.es", "lang:es"), LanguageButton(locale, "language.name.fr", "lang:fr")],
                [LanguageButton(locale, "language.name.de_pl", "lang:de_pl")]
            ]);
    }

    public TelegramInlineKeyboardMarkup BuildOnboardingSecondaryLanguageKeyboard(string locale)
    {
        return new TelegramInlineKeyboardMarkup(
            InlineKeyboard:
            [
                [LanguageButton(locale, "language.name.de", "lang:de"), LanguageButton(locale, "language.name.pl", "lang:pl")],
                [Button("back", locale, "lang:back_onboarding")]
            ]);
    }

    public TelegramInlineKeyboardMarkup BuildSettingsKeyboard(string locale)
    {
        return new TelegramInlineKeyboardMarkup(
            InlineKeyboard:
            [
                [Button("settings.change_language", locale, "settings:language")],
                [Button("settings.change_save_mode", locale, "settings:savemode")],
                [Button("settings.onedrive", locale, "settings:onedrive")],
                [Button("settings.notion", locale, "settings:notion")],
                [Button("settings.back", locale, "nav:main")]
            ]);
    }

    public TelegramInlineKeyboardMarkup BuildSettingsLanguageKeyboard(string locale)
    {
        return new TelegramInlineKeyboardMarkup(
            InlineKeyboard:
            [
                [LanguageButton(locale, "language.name.uk", "lang:uk"), LanguageButton(locale, "language.name.en", "lang:en")],
                [LanguageButton(locale, "language.name.es", "lang:es"), LanguageButton(locale, "language.name.fr", "lang:fr")],
                [LanguageButton(locale, "language.name.de_pl", "lang:de_pl")],
                [Button("back", locale, "settings:back")]
            ]);
    }

    public TelegramInlineKeyboardMarkup BuildSettingsSecondaryLanguageKeyboard(string locale)
    {
        return new TelegramInlineKeyboardMarkup(
            InlineKeyboard:
            [
                [LanguageButton(locale, "language.name.de", "lang:de"), LanguageButton(locale, "language.name.pl", "lang:pl")],
                [Button("back", locale, "settings:language")]
            ]);
    }

    public TelegramInlineKeyboardMarkup BuildSaveModeKeyboard(string locale)
    {
        return new TelegramInlineKeyboardMarkup(
            InlineKeyboard:
            [
                [Button("savemode.auto", locale, "savemode:auto"), Button("savemode.ask", locale, "savemode:ask"), Button("savemode.off", locale, "savemode:off")],
                [Button("back", locale, "settings:back")]
            ]);
    }

    public TelegramInlineKeyboardMarkup BuildOneDriveKeyboard(string locale, bool isConnected, bool includeCheckStatusButton = false)
    {
        var rows = new List<IReadOnlyList<TelegramInlineKeyboardButton>>();

        if (isConnected)
        {
            rows.Add([Button("onedrive.logout", locale, "onedrive:logout")]);
        }
        else
        {
            rows.Add([Button("onedrive.login", locale, "onedrive:login")]);

            if (includeCheckStatusButton)
            {
                rows.Add([Button("onedrive.check_status", locale, "onedrive:check_login")]);
            }
        }

        rows.Add([Button("back", locale, "settings:back")]);
        return new TelegramInlineKeyboardMarkup(rows);
    }

    public TelegramInlineKeyboardMarkup BuildNotionKeyboard(string locale)
    {
        return new TelegramInlineKeyboardMarkup(
            InlineKeyboard:
            [
                [Button("back", locale, "settings:back")]
            ]);
    }

    private TelegramInlineKeyboardButton Button(string key, string locale, string callbackData)
        => new(_localizationService.Get(key, locale), callbackData);

    private TelegramInlineKeyboardButton LanguageButton(string locale, string key, string callbackData)
        => new(_localizationService.Get(key, locale), callbackData);
}
