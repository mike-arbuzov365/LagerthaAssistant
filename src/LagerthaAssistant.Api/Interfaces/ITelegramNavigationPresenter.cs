using LagerthaAssistant.Api.Contracts;
using LagerthaAssistant.Application.Navigation;

namespace LagerthaAssistant.Api.Interfaces;

public interface ITelegramNavigationPresenter
{
    MainMenuLabels GetMainMenuLabels(string locale);

    string GetText(string key, string locale, params object[] args);

    TelegramReplyKeyboardMarkup BuildMainReplyKeyboard(string locale);

    TelegramInlineKeyboardMarkup BuildVocabularyKeyboard(string locale);

    TelegramInlineKeyboardMarkup BuildShoppingKeyboard(string locale);

    TelegramInlineKeyboardMarkup BuildWeeklyMenuKeyboard(string locale);
}
