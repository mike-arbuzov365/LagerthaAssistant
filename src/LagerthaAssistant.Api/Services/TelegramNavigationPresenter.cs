using LagerthaAssistant.Api.Contracts;
using LagerthaAssistant.Api.Interfaces;
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
            WeeklyMenu: _localizationService.Get("menu.main.menu", locale));
    }

    public string GetText(string key, string locale, params object[] args)
    {
        var template = _localizationService.Get(key, locale);
        return args.Length == 0
            ? template
            : string.Format(template, args);
    }

    public TelegramReplyKeyboardMarkup BuildMainReplyKeyboard(string locale)
    {
        var labels = GetMainMenuLabels(locale);
        return new TelegramReplyKeyboardMarkup(
            Keyboard:
            [
                [new TelegramKeyboardButton(labels.Chat), new TelegramKeyboardButton(labels.Vocabulary)],
                [new TelegramKeyboardButton(labels.Shopping), new TelegramKeyboardButton(labels.WeeklyMenu)]
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

    private TelegramInlineKeyboardButton Button(string key, string locale, string callbackData)
        => new(_localizationService.Get(key, locale), callbackData);
}
