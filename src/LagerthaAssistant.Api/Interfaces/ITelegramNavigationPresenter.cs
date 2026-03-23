using LagerthaAssistant.Api.Contracts;
using LagerthaAssistant.Application.Navigation;

namespace LagerthaAssistant.Api.Interfaces;

public interface ITelegramNavigationPresenter
{
    MainMenuLabels GetMainMenuLabels(string locale);

    string GetText(string key, string locale, params object[] args);

    string GetLanguageDisplayName(string locale);

    TelegramReplyKeyboardMarkup BuildMainReplyKeyboard(string locale);

    TelegramInlineKeyboardMarkup BuildVocabularyKeyboard(string locale);

    TelegramInlineKeyboardMarkup BuildFoodMenuKeyboard(string locale);

    TelegramInlineKeyboardMarkup BuildInventoryKeyboard(string locale);

    TelegramInlineKeyboardMarkup BuildShoppingKeyboard(string locale);

    TelegramInlineKeyboardMarkup BuildWeeklyMenuKeyboard(string locale);

    TelegramInlineKeyboardMarkup BuildOnboardingLanguageKeyboard(string locale);

    TelegramInlineKeyboardMarkup BuildOnboardingSecondaryLanguageKeyboard(string locale);

    TelegramInlineKeyboardMarkup BuildSettingsKeyboard(string locale);

    TelegramInlineKeyboardMarkup BuildSettingsLanguageKeyboard(string locale);

    TelegramInlineKeyboardMarkup BuildSettingsSecondaryLanguageKeyboard(string locale);

    TelegramInlineKeyboardMarkup BuildSaveModeKeyboard(string locale);

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
}
