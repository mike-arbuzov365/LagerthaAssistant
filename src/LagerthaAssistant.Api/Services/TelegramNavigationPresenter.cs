using LagerthaAssistant.Api.Contracts;
using LagerthaAssistant.Api.Interfaces;
using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.Interfaces;
using LagerthaAssistant.Application.Navigation;

namespace LagerthaAssistant.Api.Services;

public sealed class TelegramNavigationPresenter : ITelegramNavigationPresenter
{
    private readonly ILocalizationService _localizationService;
    private readonly string? _miniAppSettingsUrl;
    private readonly string? _miniAppSettingsDirectUrl;

    public bool CanLaunchSettingsMiniApp
        => !string.IsNullOrWhiteSpace(_miniAppSettingsDirectUrl)
           || !string.IsNullOrWhiteSpace(_miniAppSettingsUrl);

    public TelegramNavigationPresenter(
        ILocalizationService localizationService,
        string? miniAppSettingsUrl = null,
        string? miniAppSettingsDirectUrl = null)
    {
        _localizationService = localizationService;
        _miniAppSettingsUrl = NormalizeMiniAppSettingsUrl(miniAppSettingsUrl);
        _miniAppSettingsDirectUrl = NormalizeMiniAppSettingsDirectUrl(miniAppSettingsDirectUrl);
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
                [Button("menu.vocabulary.add", locale, CallbackDataConstants.Vocab.Add), Button("menu.vocabulary.batch", locale, CallbackDataConstants.Vocab.Batch)],
                [Button("menu.vocabulary.url", locale, CallbackDataConstants.Vocab.Url), Button("menu.vocabulary.stats", locale, CallbackDataConstants.Vocab.Stats)],
                [Button("menu.vocabulary.back", locale, CallbackDataConstants.Nav.Main)]
            ]);
    }

    public TelegramInlineKeyboardMarkup BuildFoodMenuKeyboard(string locale)
    {
        return new TelegramInlineKeyboardMarkup(
            InlineKeyboard:
            [
                [Button("menu.food.inventory", locale, CallbackDataConstants.Food.Inventory), Button("menu.food.shopping", locale, CallbackDataConstants.Food.Shopping)],
                [Button("menu.food.back", locale, CallbackDataConstants.Nav.Main)]
            ]);
    }

    public TelegramInlineKeyboardMarkup BuildInventoryKeyboard(string locale)
    {
        return new TelegramInlineKeyboardMarkup(
            InlineKeyboard:
            [
                [Button("menu.inventory.list", locale, CallbackDataConstants.Inventory.List), Button("menu.inventory.search", locale, CallbackDataConstants.Inventory.Search)],
                [Button("menu.inventory.suggest", locale, CallbackDataConstants.Inventory.Suggest), Button("menu.inventory.stats", locale, CallbackDataConstants.Inventory.Stats)],
                [Button("menu.inventory.move", locale, CallbackDataConstants.Inventory.Move), Button("menu.inventory.manage", locale, CallbackDataConstants.Inventory.Manage)],
                [Button("menu.food.shopping", locale, CallbackDataConstants.Food.Shopping)],
                [Button("menu.inventory.back", locale, CallbackDataConstants.Nav.Main)]
            ]);
    }

    public TelegramInlineKeyboardMarkup BuildInventoryMoveKeyboard(string locale)
    {
        return new TelegramInlineKeyboardMarkup(
            InlineKeyboard:
            [
                [Button("menu.inventory.photo_restock", locale, CallbackDataConstants.Inventory.PhotoRestock)],
                [Button("menu.inventory.photo_consume", locale, CallbackDataConstants.Inventory.PhotoConsume)],
                [Button("menu.inventory.sub.back", locale, CallbackDataConstants.Food.Inventory)]
            ]);
    }

    public TelegramInlineKeyboardMarkup BuildInventoryManageKeyboard(string locale)
    {
        return new TelegramInlineKeyboardMarkup(
            InlineKeyboard:
            [
                [Button("menu.inventory.adjust", locale, CallbackDataConstants.Inventory.Adjust), Button("menu.inventory.min", locale, CallbackDataConstants.Inventory.Min)],
                [Button("menu.inventory.reset_stock", locale, CallbackDataConstants.Inventory.ResetStock)],
                [Button("menu.inventory.sub.back", locale, CallbackDataConstants.Food.Inventory)]
            ]);
    }

    public TelegramInlineKeyboardMarkup BuildShoppingKeyboard(string locale)
    {
        return new TelegramInlineKeyboardMarkup(
            InlineKeyboard:
            [
                [Button("menu.shopping.add", locale, CallbackDataConstants.Shop.Add), Button("menu.shopping.list", locale, CallbackDataConstants.Shop.List)],
                [Button("menu.shopping.delete", locale, CallbackDataConstants.Shop.Delete)],
                [Button("menu.shopping.back", locale, CallbackDataConstants.Food.Inventory)]
            ]);
    }

    public TelegramInlineKeyboardMarkup BuildWeeklyMenuKeyboard(string locale)
    {
        return new TelegramInlineKeyboardMarkup(
            InlineKeyboard:
            [
                [Button("menu.weekly.view", locale, CallbackDataConstants.Weekly.View), Button("menu.weekly.plan", locale, CallbackDataConstants.Weekly.Plan)],
                [Button("menu.weekly.log", locale, CallbackDataConstants.Weekly.Log), Button("menu.weekly.create", locale, CallbackDataConstants.Weekly.Create)],
                [Button("menu.weekly.analytics", locale, CallbackDataConstants.Weekly.Analytics)],
                [Button("menu.weekly.back", locale, CallbackDataConstants.Nav.Main)]
            ]);
    }

    public TelegramInlineKeyboardMarkup BuildWeeklyAnalyticsKeyboard(string locale)
    {
        return new TelegramInlineKeyboardMarkup(
            InlineKeyboard:
            [
                [Button("menu.weekly.calories", locale, CallbackDataConstants.Weekly.Calories), Button("menu.weekly.goal", locale, CallbackDataConstants.Weekly.DailyGoal)],
                [Button("menu.weekly.favourites", locale, CallbackDataConstants.Weekly.Favourites), Button("menu.weekly.diversity", locale, CallbackDataConstants.Weekly.Diversity)],
                [Button("menu.weekly.analytics.back", locale, CallbackDataConstants.Nav.Weekly)]
            ]);
    }

    public TelegramInlineKeyboardMarkup BuildOnboardingLanguageKeyboard(string locale)
    {
        return new TelegramInlineKeyboardMarkup(
            InlineKeyboard:
            [
                [LanguageButton(locale, "language.name.uk", CallbackDataConstants.Lang.Ukrainian), LanguageButton(locale, "language.name.en", CallbackDataConstants.Lang.English)]
            ]);
    }

    public TelegramInlineKeyboardMarkup BuildSettingsLaunchKeyboard(string locale)
    {
        var rows = new List<IReadOnlyList<TelegramInlineKeyboardButton>>();

        if (!string.IsNullOrWhiteSpace(_miniAppSettingsDirectUrl))
        {
            rows.Add([
                UrlButton(
                    locale,
                    "settings.launch_open",
                    _miniAppSettingsDirectUrl)
            ]);
        }
        else if (!string.IsNullOrWhiteSpace(_miniAppSettingsUrl))
        {
            rows.Add([
                WebAppButton(
                    locale,
                    "settings.launch_open",
                    _miniAppSettingsUrl)
            ]);
        }

        rows.Add([Button("settings.back", locale, CallbackDataConstants.Nav.Main)]);

        return new TelegramInlineKeyboardMarkup(rows);
    }

    public TelegramInlineKeyboardMarkup BuildSettingsKeyboard(string locale)
    {
        return new TelegramInlineKeyboardMarkup(
            InlineKeyboard:
            [
                [Button("settings.change_language", locale, CallbackDataConstants.Settings.Language)],
                [Button("settings.change_save_mode", locale, CallbackDataConstants.Settings.SaveMode)],
                [Button("settings.change_ai", locale, CallbackDataConstants.Settings.Ai)],
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
                [Button("back", locale, CallbackDataConstants.Settings.Back)]
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
            rows.Add([Button("onedrive.clear_cache", locale, CallbackDataConstants.OneDrive.ClearCache)]);
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

    public TelegramInlineKeyboardMarkup BuildOneDriveClearCacheConfirmationKeyboard(string locale)
    {
        return new TelegramInlineKeyboardMarkup(
            InlineKeyboard:
            [
                [Button("onedrive.clear_cache_start", locale, CallbackDataConstants.OneDrive.ClearCacheConfirm)],
                [Button("back", locale, CallbackDataConstants.Settings.OneDrive)]
            ]);
    }

    public TelegramInlineKeyboardMarkup BuildMealCreateConfirmKeyboard(string locale)
    {
        return new TelegramInlineKeyboardMarkup(
            InlineKeyboard:
            [
                [Button("meal.create.confirm", locale, CallbackDataConstants.Weekly.CreateConfirm), Button("meal.create.cancel", locale, CallbackDataConstants.Weekly.CreateCancel)]
            ]);
    }

    public TelegramInlineKeyboardMarkup BuildFoodPhotoConfirmKeyboard(string locale)
    {
        return new TelegramInlineKeyboardMarkup(
            InlineKeyboard:
            [
                [Button("food.photo.confirm", locale, CallbackDataConstants.Weekly.PhotoConfirm), Button("food.photo.cancel", locale, CallbackDataConstants.Weekly.PhotoCancel)]
            ]);
    }

    public TelegramInlineKeyboardMarkup BuildInventoryPhotoConfirmKeyboard(string locale)
    {
        return new TelegramInlineKeyboardMarkup(
            InlineKeyboard:
            [
                [Button("inventory.photo.apply_all", locale, CallbackDataConstants.Inventory.PhotoApplyAll)],
                [Button("inventory.photo.select", locale, CallbackDataConstants.Inventory.PhotoSelect), Button("inventory.photo.cancel", locale, CallbackDataConstants.Inventory.PhotoCancel)]
            ]);
    }

    public TelegramInlineKeyboardMarkup BuildInventoryResetStockConfirmationKeyboard(string locale)
    {
        return new TelegramInlineKeyboardMarkup(
            InlineKeyboard:
            [
                [Button("inventory.reset_stock.confirm", locale, CallbackDataConstants.Inventory.ResetStockConfirm)],
                [Button("back", locale, CallbackDataConstants.Food.Inventory)]
            ]);
    }

    public TelegramInlineKeyboardMarkup BuildPhotoStoreResolutionKeyboard(string locale, string storeNameEn)
    {
        return new TelegramInlineKeyboardMarkup(
            InlineKeyboard:
            [
                [Button("inventory.photo.store.add", locale, CallbackDataConstants.Inventory.PhotoStoreAdd)],
                [Button("inventory.photo.store.pick_existing", locale, CallbackDataConstants.Inventory.PhotoStorePickExisting)],
                [Button("inventory.photo.store.skip", locale, CallbackDataConstants.Inventory.PhotoStoreSkip)]
            ]);
    }

    public TelegramInlineKeyboardMarkup BuildPhotoStorePickExistingKeyboard(string locale, IReadOnlyList<string> stores)
    {
        var rows = new List<IReadOnlyList<TelegramInlineKeyboardButton>>();
        foreach (var store in stores)
        {
            rows.Add([new TelegramInlineKeyboardButton(store, CallbackDataConstants.Inventory.PhotoStoreSelectPrefix + store)]);
        }
        rows.Add([Button("inventory.photo.store.skip", locale, CallbackDataConstants.Inventory.PhotoStoreSkip)]);
        return new TelegramInlineKeyboardMarkup(rows);
    }

    public TelegramInlineKeyboardMarkup BuildPhotoUnknownItemsKeyboard(string locale)
    {
        return new TelegramInlineKeyboardMarkup(
            InlineKeyboard:
            [
                [Button("inventory.photo.unknown.add_all", locale, CallbackDataConstants.Inventory.PhotoUnknownAddAll)],
                [
                    Button("inventory.photo.unknown.select", locale, CallbackDataConstants.Inventory.PhotoUnknownSelect),
                    Button("inventory.photo.unknown.link", locale, CallbackDataConstants.Inventory.PhotoUnknownLink)
                ],
                [Button("inventory.photo.unknown.skip", locale, CallbackDataConstants.Inventory.PhotoUnknownSkip)]
            ]);
    }

    public TelegramInlineKeyboardMarkup BuildAiSettingsKeyboard(string locale)
    {
        return new TelegramInlineKeyboardMarkup(
            InlineKeyboard:
            [
                [Button("ai.provider.change", locale, CallbackDataConstants.Ai.Provider)],
                [Button("ai.model.change", locale, CallbackDataConstants.Ai.Model)],
                [Button("ai.key.set", locale, CallbackDataConstants.Ai.KeySet)],
                [Button("ai.key.remove", locale, CallbackDataConstants.Ai.KeyRemove)],
                [Button("back", locale, CallbackDataConstants.Ai.Back)]
            ]);
    }

    public TelegramInlineKeyboardMarkup BuildAiProviderKeyboard(string locale)
    {
        return new TelegramInlineKeyboardMarkup(
            InlineKeyboard:
            [
                [new TelegramInlineKeyboardButton("OpenAI", $"{CallbackDataConstants.Ai.ProviderSetPrefix}openai")],
                [new TelegramInlineKeyboardButton("Claude", $"{CallbackDataConstants.Ai.ProviderSetPrefix}claude")],
                [Button("back", locale, CallbackDataConstants.Ai.Back)]
            ]);
    }

    public TelegramInlineKeyboardMarkup BuildAiModelKeyboard(string locale, IReadOnlyList<string> models)
    {
        var rows = models
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(model => (IReadOnlyList<TelegramInlineKeyboardButton>)
            [
                new TelegramInlineKeyboardButton(model, $"{CallbackDataConstants.Ai.ModelSetPrefix}{model}")
            ])
            .ToList();

        rows.Add([Button("back", locale, CallbackDataConstants.Ai.Back)]);
        return new TelegramInlineKeyboardMarkup(rows);
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

    public TelegramInlineKeyboardMarkup BuildVocabularyBatchSaveConfirmationKeyboard(string locale)
    {
        return new TelegramInlineKeyboardMarkup(
            InlineKeyboard:
            [
                [Button("vocab.save_batch_yes", locale, CallbackDataConstants.Vocab.SaveBatchYes)],
                [Button("vocab.save_batch_no", locale, CallbackDataConstants.Vocab.SaveBatchNo)]
            ]);
    }

    public TelegramInlineKeyboardMarkup BuildVocabularyImportSourceKeyboard(string locale)
    {
        return new TelegramInlineKeyboardMarkup(
            InlineKeyboard:
            [
                [Button("vocab.import.source.photo", locale, CallbackDataConstants.Vocab.ImportSourcePhoto), Button("vocab.import.source.file", locale, CallbackDataConstants.Vocab.ImportSourceFile)],
                [Button("vocab.import.source.url", locale, CallbackDataConstants.Vocab.ImportSourceUrl), Button("vocab.import.source.text", locale, CallbackDataConstants.Vocab.ImportSourceText)],
                [Button("menu.vocabulary.back", locale, CallbackDataConstants.Nav.Main)]
            ]);
    }

    public TelegramInlineKeyboardMarkup BuildVocabularyUrlSelectionKeyboard(string locale)
    {
        return new TelegramInlineKeyboardMarkup(
            InlineKeyboard:
            [
                [Button("vocab.url.select_all", locale, CallbackDataConstants.Vocab.UrlSelectAll)],
                [Button("vocab.url.cancel", locale, CallbackDataConstants.Vocab.UrlCancel)],
                [Button("menu.vocabulary.back", locale, CallbackDataConstants.Nav.Main)]
            ]);
    }

    private TelegramInlineKeyboardButton Button(string key, string locale, string callbackData)
        => new(_localizationService.Get(key, locale), callbackData);

    private TelegramInlineKeyboardButton UrlButton(string locale, string key, string url)
        => new(_localizationService.Get(key, locale), Url: url);

    private TelegramInlineKeyboardButton WebAppButton(string locale, string key, string url)
        => new(_localizationService.Get(key, locale), WebApp: new TelegramWebAppInfo(url));

    private TelegramInlineKeyboardButton LanguageButton(string locale, string key, string callbackData)
        => new(_localizationService.Get(key, locale), callbackData);

    private static string? NormalizeMiniAppSettingsUrl(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var trimmed = raw.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return null;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return uri.AbsoluteUri;
    }

    private static string? NormalizeMiniAppSettingsDirectUrl(string? raw)
        => NormalizeMiniAppSettingsUrl(raw);
}
