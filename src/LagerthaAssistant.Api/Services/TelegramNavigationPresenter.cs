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
                [Button("menu.vocabulary.add", locale, CallbackDataConstants.Vocab.Add), Button("menu.vocabulary.list", locale, CallbackDataConstants.Vocab.List)],
                [Button("menu.vocabulary.url", locale, CallbackDataConstants.Vocab.Url), Button("menu.vocabulary.batch", locale, CallbackDataConstants.Vocab.Batch)],
                [Button("menu.vocabulary.back", locale, CallbackDataConstants.Nav.Main)]
            ]);
    }

    public TelegramInlineKeyboardMarkup BuildShoppingKeyboard(string locale)
    {
        return new TelegramInlineKeyboardMarkup(
            InlineKeyboard:
            [
                [Button("menu.shopping.add", locale, CallbackDataConstants.Shop.Add), Button("menu.shopping.list", locale, CallbackDataConstants.Shop.List)],
                [Button("menu.shopping.delete", locale, CallbackDataConstants.Shop.Delete)],
                [Button("menu.shopping.back", locale, CallbackDataConstants.Nav.Main)]
            ]);
    }

    public TelegramInlineKeyboardMarkup BuildWeeklyMenuKeyboard(string locale)
    {
        return new TelegramInlineKeyboardMarkup(
            InlineKeyboard:
            [
                [Button("menu.weekly.view", locale, CallbackDataConstants.Weekly.View), Button("menu.weekly.plan", locale, CallbackDataConstants.Weekly.Plan)],
                [Button("menu.weekly.calories", locale, CallbackDataConstants.Weekly.Calories)],
                [Button("menu.weekly.back", locale, CallbackDataConstants.Nav.Main)]
            ]);
    }

    public TelegramInlineKeyboardMarkup BuildOnboardingLanguageKeyboard(string locale)
    {
        return new TelegramInlineKeyboardMarkup(
            InlineKeyboard:
            [
                [LanguageButton(locale, "language.name.uk", CallbackDataConstants.Lang.Ukrainian), LanguageButton(locale, "language.name.en", CallbackDataConstants.Lang.English)],
                [LanguageButton(locale, "language.name.es", CallbackDataConstants.Lang.Spanish), LanguageButton(locale, "language.name.fr", CallbackDataConstants.Lang.French)],
                [LanguageButton(locale, "language.name.de_pl", CallbackDataConstants.Lang.GermanPolish)]
            ]);
    }

    public TelegramInlineKeyboardMarkup BuildOnboardingSecondaryLanguageKeyboard(string locale)
    {
        return new TelegramInlineKeyboardMarkup(
            InlineKeyboard:
            [
                [LanguageButton(locale, "language.name.de", CallbackDataConstants.Lang.German), LanguageButton(locale, "language.name.pl", CallbackDataConstants.Lang.Polish)],
                [Button("back", locale, CallbackDataConstants.Lang.BackOnboarding)]
            ]);
    }

    public TelegramInlineKeyboardMarkup BuildSettingsKeyboard(string locale)
    {
        return new TelegramInlineKeyboardMarkup(
            InlineKeyboard:
            [
                [Button("settings.change_language", locale, CallbackDataConstants.Settings.Language)],
                [Button("settings.change_save_mode", locale, CallbackDataConstants.Settings.SaveMode)],
                [Button("settings.onedrive", locale, CallbackDataConstants.Settings.OneDrive)],
                [Button("settings.notion", locale, CallbackDataConstants.Settings.Notion)],
                [Button("settings.back", locale, CallbackDataConstants.Nav.Main)]
            ]);
    }

    public TelegramInlineKeyboardMarkup BuildSettingsLanguageKeyboard(string locale)
    {
        return new TelegramInlineKeyboardMarkup(
            InlineKeyboard:
            [
                [LanguageButton(locale, "language.name.uk", CallbackDataConstants.Lang.Ukrainian), LanguageButton(locale, "language.name.en", CallbackDataConstants.Lang.English)],
                [LanguageButton(locale, "language.name.es", CallbackDataConstants.Lang.Spanish), LanguageButton(locale, "language.name.fr", CallbackDataConstants.Lang.French)],
                [LanguageButton(locale, "language.name.de_pl", CallbackDataConstants.Lang.GermanPolish)],
                [Button("back", locale, CallbackDataConstants.Settings.Back)]
            ]);
    }

    public TelegramInlineKeyboardMarkup BuildSettingsSecondaryLanguageKeyboard(string locale)
    {
        return new TelegramInlineKeyboardMarkup(
            InlineKeyboard:
            [
                [LanguageButton(locale, "language.name.de", CallbackDataConstants.Lang.German), LanguageButton(locale, "language.name.pl", CallbackDataConstants.Lang.Polish)],
                [Button("back", locale, CallbackDataConstants.Settings.Language)]
            ]);
    }

    public TelegramInlineKeyboardMarkup BuildSaveModeKeyboard(string locale)
    {
        return new TelegramInlineKeyboardMarkup(
            InlineKeyboard:
            [
                [Button("savemode.auto", locale, CallbackDataConstants.SaveMode.Auto), Button("savemode.ask", locale, CallbackDataConstants.SaveMode.Ask), Button("savemode.off", locale, CallbackDataConstants.SaveMode.Off)],
                [Button("back", locale, CallbackDataConstants.Settings.Back)]
            ]);
    }

    public TelegramInlineKeyboardMarkup BuildOneDriveKeyboard(string locale, bool isConnected, bool includeCheckStatusButton = false)
    {
        var rows = new List<IReadOnlyList<TelegramInlineKeyboardButton>>();

        if (isConnected)
        {
            rows.Add([Button("onedrive.logout", locale, CallbackDataConstants.OneDrive.Logout)]);
            rows.Add([Button("onedrive.sync_now", locale, CallbackDataConstants.OneDrive.SyncNow)]);
            rows.Add([Button("onedrive.rebuild_index", locale, CallbackDataConstants.OneDrive.RebuildIndex)]);
        }
        else
        {
            rows.Add([Button("onedrive.login", locale, CallbackDataConstants.OneDrive.Login)]);

            if (includeCheckStatusButton)
            {
                rows.Add([Button("onedrive.check_status", locale, CallbackDataConstants.OneDrive.CheckLogin)]);
            }
        }

        rows.Add([Button("back", locale, CallbackDataConstants.Settings.Back)]);
        return new TelegramInlineKeyboardMarkup(rows);
    }

    public TelegramInlineKeyboardMarkup BuildOneDriveRebuildIndexConfirmationKeyboard(string locale)
    {
        return new TelegramInlineKeyboardMarkup(
            InlineKeyboard:
            [
                [Button("onedrive.rebuild_index_start", locale, CallbackDataConstants.OneDrive.RebuildIndexConfirm)],
                [Button("back", locale, CallbackDataConstants.Settings.OneDrive)]
            ]);
    }

    public TelegramInlineKeyboardMarkup BuildNotionKeyboard(string locale)
    {
        return new TelegramInlineKeyboardMarkup(
            InlineKeyboard:
            [
                [Button("back", locale, CallbackDataConstants.Settings.Back)]
            ]);
    }

    public TelegramInlineKeyboardMarkup BuildVocabularySaveConfirmationKeyboard(string locale)
    {
        return new TelegramInlineKeyboardMarkup(
            InlineKeyboard:
            [
                [Button("vocab.save_yes", locale, CallbackDataConstants.Vocab.SaveYes), Button("vocab.save_no", locale, CallbackDataConstants.Vocab.SaveNo)]
            ]);
    }

    private TelegramInlineKeyboardButton Button(string key, string locale, string callbackData)
        => new(_localizationService.Get(key, locale), callbackData);

    private TelegramInlineKeyboardButton LanguageButton(string locale, string key, string callbackData)
        => new(_localizationService.Get(key, locale), callbackData);
}
