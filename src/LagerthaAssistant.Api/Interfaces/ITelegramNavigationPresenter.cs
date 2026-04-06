using LagerthaAssistant.Api.Contracts;
using LagerthaAssistant.Application.Navigation;

namespace LagerthaAssistant.Api.Interfaces;

public interface ITelegramNavigationPresenter
{
    bool CanLaunchSettingsMiniApp { get; }

    MainMenuLabels GetMainMenuLabels(string locale);

    string GetText(string key, string locale, params object[] args);

    string GetLanguageDisplayName(string locale);

    TelegramReplyKeyboardMarkup BuildMainReplyKeyboard(string locale);

    TelegramInlineKeyboardMarkup BuildVocabularyKeyboard(string locale);

    TelegramInlineKeyboardMarkup BuildFoodMenuKeyboard(string locale);

    TelegramInlineKeyboardMarkup BuildInventoryKeyboard(string locale);

    TelegramInlineKeyboardMarkup BuildInventoryMoveKeyboard(string locale);

    TelegramInlineKeyboardMarkup BuildInventoryManageKeyboard(string locale);

    TelegramInlineKeyboardMarkup BuildShoppingKeyboard(string locale);

    TelegramInlineKeyboardMarkup BuildWeeklyMenuKeyboard(string locale);

    TelegramInlineKeyboardMarkup BuildWeeklyAnalyticsKeyboard(string locale);

    TelegramInlineKeyboardMarkup BuildOnboardingLanguageKeyboard(string locale);

    TelegramInlineKeyboardMarkup BuildSettingsLaunchKeyboard(string locale);

    TelegramInlineKeyboardMarkup BuildSettingsKeyboard(string locale);

    TelegramInlineKeyboardMarkup BuildSettingsLanguageKeyboard(string locale);

    TelegramInlineKeyboardMarkup BuildSaveModeKeyboard(string locale);

    TelegramInlineKeyboardMarkup BuildAiSettingsKeyboard(string locale);

    TelegramInlineKeyboardMarkup BuildAiProviderKeyboard(string locale);

    TelegramInlineKeyboardMarkup BuildAiModelKeyboard(string locale, IReadOnlyList<string> models);

    TelegramInlineKeyboardMarkup BuildOneDriveKeyboard(string locale, bool isConnected, bool includeCheckStatusButton = false);

    TelegramInlineKeyboardMarkup BuildOneDriveRebuildIndexConfirmationKeyboard(string locale);

    TelegramInlineKeyboardMarkup BuildOneDriveClearCacheConfirmationKeyboard(string locale);

    TelegramInlineKeyboardMarkup BuildVocabularySaveConfirmationKeyboard(string locale);

    TelegramInlineKeyboardMarkup BuildVocabularyBatchSaveConfirmationKeyboard(string locale);

    TelegramInlineKeyboardMarkup BuildVocabularyImportSourceKeyboard(string locale);

    TelegramInlineKeyboardMarkup BuildVocabularyUrlSelectionKeyboard(string locale);

    TelegramInlineKeyboardMarkup BuildNotionKeyboard(string locale);

    TelegramInlineKeyboardMarkup BuildMealCreateConfirmKeyboard(string locale);

    TelegramInlineKeyboardMarkup BuildFoodPhotoConfirmKeyboard(string locale);

    TelegramInlineKeyboardMarkup BuildInventoryPhotoConfirmKeyboard(string locale);

    TelegramInlineKeyboardMarkup BuildInventoryResetStockConfirmationKeyboard(string locale);

    TelegramInlineKeyboardMarkup BuildPhotoStoreResolutionKeyboard(string locale, string storeNameEn);

    TelegramInlineKeyboardMarkup BuildPhotoStorePickExistingKeyboard(string locale, IReadOnlyList<string> stores);

    TelegramInlineKeyboardMarkup BuildPhotoUnknownItemsKeyboard(string locale);

    TelegramInlineKeyboardMarkup BuildInputOnlyBackKeyboard(string locale, string callbackData);

    TelegramInlineKeyboardMarkup BuildMediaIntentKeyboard(string locale, IReadOnlyList<TelegramMediaCapability> capabilities, string backCallbackData);
}
