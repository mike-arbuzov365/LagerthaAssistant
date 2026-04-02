namespace LagerthaAssistant.IntegrationTests.Services;

using LagerthaAssistant.Api.Services;
using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Infrastructure.Services;
using Xunit;

public sealed class TelegramNavigationPresenterTests
{
    [Fact]
    public void BuildMainReplyKeyboard_ShouldIncludeSettingsRow()
    {
        var sut = new TelegramNavigationPresenter(new LocalizationService());

        var keyboard = sut.BuildMainReplyKeyboard("en");

        Assert.Equal(3, keyboard.Keyboard.Count);
        Assert.Single(keyboard.Keyboard[2]);
        Assert.Contains("Settings", keyboard.Keyboard[2][0].Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildMainReplyKeyboard_ShouldKeepSettingsAsPlainText_WhenMiniAppUrlConfigured()
    {
        var sut = new TelegramNavigationPresenter(
            new LocalizationService(),
            "https://lagertha.example.com/miniapp/settings");

        var keyboard = sut.BuildMainReplyKeyboard("uk");
        var settingsButton = keyboard.Keyboard[2][0];

        Assert.Null(settingsButton.WebApp);
        Assert.Equal("⚙️ Налаштування", settingsButton.Text);
    }

    [Fact]
    public void BuildSettingsLaunchKeyboard_ShouldPreferDirectUrl_WhenConfigured()
    {
        var sut = new TelegramNavigationPresenter(
            new LocalizationService(),
            "https://lagertha.example.com/miniapp/settings",
            "https://t.me/LagerthaAssistantBot?startapp=settings&mode=fullscreen");

        var keyboard = sut.BuildSettingsLaunchKeyboard("en");

        Assert.Equal("https://t.me/LagerthaAssistantBot?startapp=settings&mode=fullscreen", keyboard.InlineKeyboard[0][0].Url);
        Assert.Null(keyboard.InlineKeyboard[0][0].WebApp);
        Assert.Equal(CallbackDataConstants.Settings.Legacy, keyboard.InlineKeyboard[1][0].CallbackData);
    }

    [Fact]
    public void BuildSettingsLaunchKeyboard_ShouldFallbackToWebApp_WhenDirectUrlMissing()
    {
        var sut = new TelegramNavigationPresenter(
            new LocalizationService(),
            "https://lagertha.example.com/miniapp/settings");

        var keyboard = sut.BuildSettingsLaunchKeyboard("en");

        Assert.NotNull(keyboard.InlineKeyboard[0][0].WebApp);
        Assert.Equal("https://lagertha.example.com/miniapp/settings", keyboard.InlineKeyboard[0][0].WebApp!.Url);
        Assert.Equal(CallbackDataConstants.Settings.Legacy, keyboard.InlineKeyboard[1][0].CallbackData);
    }

    [Fact]
    public void BuildMainReplyKeyboard_ShouldUseEnglishLabels_ForEnglishLocale()
    {
        var sut = new TelegramNavigationPresenter(new LocalizationService());

        var keyboard = sut.BuildMainReplyKeyboard("en");
        var labels = keyboard.Keyboard
            .SelectMany(row => row)
            .Select(button => button.Text)
            .ToList();

        Assert.Contains(labels, x => x.Contains("Vocabulary", StringComparison.Ordinal));
        Assert.DoesNotContain(labels, x => x.Contains("Словник", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildOnboardingLanguageKeyboard_ShouldIncludeOnlyUkrainianAndEnglish()
    {
        var sut = new TelegramNavigationPresenter(new LocalizationService());

        var keyboard = sut.BuildOnboardingLanguageKeyboard("en");
        var callbacks = keyboard.InlineKeyboard
            .SelectMany(row => row)
            .Select(button => button.CallbackData)
            .ToList();

        Assert.Equal(2, callbacks.Count);
        Assert.Contains("lang:uk", callbacks);
        Assert.Contains("lang:en", callbacks);
    }

    [Fact]
    public void BuildSettingsKeyboard_ShouldContainExpectedCallbacks()
    {
        var sut = new TelegramNavigationPresenter(new LocalizationService());

        var keyboard = sut.BuildSettingsKeyboard("en");
        var callbacks = keyboard.InlineKeyboard
            .SelectMany(row => row)
            .Select(button => button.CallbackData)
            .ToList();

        Assert.Contains("settings:language", callbacks);
        Assert.Contains("settings:savemode", callbacks);
        Assert.Contains("settings:onedrive", callbacks);
        Assert.Contains("settings:notion", callbacks);
        Assert.Contains("nav:main", callbacks);
    }

    [Fact]
    public void BuildVocabularyKeyboard_ShouldPlaceBatchBeforeStatistics()
    {
        var sut = new TelegramNavigationPresenter(new LocalizationService());

        var keyboard = sut.BuildVocabularyKeyboard("uk");

        Assert.Equal("vocab:add", keyboard.InlineKeyboard[0][0].CallbackData);
        Assert.Equal("vocab:batch", keyboard.InlineKeyboard[0][1].CallbackData);
        Assert.Equal("vocab:url", keyboard.InlineKeyboard[1][0].CallbackData);
        Assert.Equal("vocab:stats", keyboard.InlineKeyboard[1][1].CallbackData);
        Assert.Contains("Стат", keyboard.InlineKeyboard[1][1].Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildShoppingKeyboard_ShouldContainExpectedCallbacks()
    {
        var sut = new TelegramNavigationPresenter(new LocalizationService());

        var keyboard = sut.BuildShoppingKeyboard("en");
        var callbacks = keyboard.InlineKeyboard
            .SelectMany(row => row)
            .Select(b => b.CallbackData)
            .ToList();

        Assert.Contains("shop:add", callbacks);
        Assert.Contains("shop:list", callbacks);
        Assert.Contains("shop:delete", callbacks);
        Assert.Contains("food:inventory", callbacks);
    }

    [Fact]
    public void BuildWeeklyMenuKeyboard_ShouldContainAllFoodCallbacks()
    {
        var sut = new TelegramNavigationPresenter(new LocalizationService());

        var keyboard = sut.BuildWeeklyMenuKeyboard("en");
        var callbacks = keyboard.InlineKeyboard
            .SelectMany(row => row)
            .Select(b => b.CallbackData)
            .ToList();

        Assert.Contains("weekly:view", callbacks);
        Assert.Contains("weekly:plan", callbacks);
        Assert.Contains("weekly:log", callbacks);
        Assert.Contains("weekly:create", callbacks);
        Assert.Contains("weekly:analytics", callbacks);
        Assert.Contains("nav:main", callbacks);
    }

    [Fact]
    public void BuildWeeklyMenuKeyboard_ShouldLocalizeButtons_ForUkrainian()
    {
        var sut = new TelegramNavigationPresenter(new LocalizationService());

        var keyboard = sut.BuildWeeklyMenuKeyboard("uk");
        var allText = keyboard.InlineKeyboard
            .SelectMany(row => row)
            .Select(b => b.Text)
            .ToList();

        Assert.Contains(allText, t => t.Contains("Записати", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(allText, t => t.Contains("Аналітика", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildInventoryManageKeyboard_ShouldContainMinStockCallback()
    {
        var sut = new TelegramNavigationPresenter(new LocalizationService());

        var keyboard = sut.BuildInventoryManageKeyboard("en");
        var callbacks = keyboard.InlineKeyboard
            .SelectMany(row => row)
            .Select(button => button.CallbackData)
            .ToList();

        Assert.Contains(CallbackDataConstants.Inventory.Min, callbacks);
        Assert.Contains(CallbackDataConstants.Inventory.ResetStock, callbacks);
        Assert.Contains(CallbackDataConstants.Inventory.Adjust, callbacks);
    }

    [Fact]
    public void BuildInventoryPhotoConfirmKeyboard_ShouldContainExpectedCallbacks()
    {
        var sut = new TelegramNavigationPresenter(new LocalizationService());

        var keyboard = sut.BuildInventoryPhotoConfirmKeyboard("en");
        var callbacks = keyboard.InlineKeyboard
            .SelectMany(row => row)
            .Select(button => button.CallbackData)
            .ToList();

        Assert.Contains(CallbackDataConstants.Inventory.PhotoApplyAll, callbacks);
        Assert.Contains(CallbackDataConstants.Inventory.PhotoSelect, callbacks);
        Assert.Contains(CallbackDataConstants.Inventory.PhotoCancel, callbacks);
    }

    [Fact]
    public void BuildInventoryResetStockConfirmationKeyboard_ShouldContainExpectedCallbacks()
    {
        var sut = new TelegramNavigationPresenter(new LocalizationService());

        var keyboard = sut.BuildInventoryResetStockConfirmationKeyboard("uk");
        var callbacks = keyboard.InlineKeyboard
            .SelectMany(row => row)
            .Select(button => button.CallbackData)
            .ToList();

        Assert.Contains(CallbackDataConstants.Inventory.ResetStockConfirm, callbacks);
        Assert.Contains(CallbackDataConstants.Food.Inventory, callbacks);
    }

    [Fact]
    public void BuildPhotoUnknownItemsKeyboard_ShouldContainLinkCallback()
    {
        var sut = new TelegramNavigationPresenter(new LocalizationService());

        var keyboard = sut.BuildPhotoUnknownItemsKeyboard("uk");
        var callbacks = keyboard.InlineKeyboard
            .SelectMany(row => row)
            .Select(button => button.CallbackData)
            .ToList();

        Assert.Contains(CallbackDataConstants.Inventory.PhotoUnknownAddAll, callbacks);
        Assert.Contains(CallbackDataConstants.Inventory.PhotoUnknownSelect, callbacks);
        Assert.Contains(CallbackDataConstants.Inventory.PhotoUnknownLink, callbacks);
        Assert.Contains(CallbackDataConstants.Inventory.PhotoUnknownSkip, callbacks);
    }
}
