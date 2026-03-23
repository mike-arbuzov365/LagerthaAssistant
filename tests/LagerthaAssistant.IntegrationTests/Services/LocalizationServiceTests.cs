namespace LagerthaAssistant.IntegrationTests.Services;

using System.Reflection;
using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Infrastructure.Services;
using Xunit;

public sealed class LocalizationServiceTests
{
    [Theory]
    [InlineData(null, LocalizationConstants.EnglishLocale)]
    [InlineData("", LocalizationConstants.EnglishLocale)]
    [InlineData("en", LocalizationConstants.EnglishLocale)]
    [InlineData("en-US", LocalizationConstants.EnglishLocale)]
    [InlineData("uk", LocalizationConstants.UkrainianLocale)]
    [InlineData("uk-UA", LocalizationConstants.UkrainianLocale)]
    [InlineData("ru", LocalizationConstants.UkrainianLocale)]
    [InlineData("ru-RU", LocalizationConstants.UkrainianLocale)]
    [InlineData("be", LocalizationConstants.UkrainianLocale)]
    [InlineData("de", LocalizationConstants.GermanLocale)]
    [InlineData("fr", LocalizationConstants.FrenchLocale)]
    [InlineData("es", LocalizationConstants.SpanishLocale)]
    [InlineData("pl", LocalizationConstants.PolishLocale)]
    public void GetLocaleForUser_ShouldApplyExpectedMapping(string? languageCode, string expected)
    {
        var sut = new LocalizationService();

        var locale = sut.GetLocaleForUser(languageCode);

        Assert.Equal(expected, locale);
    }

    [Theory]
    [InlineData("menu.main.title", "en", "What can I help you with?")]
    public void Get_ShouldReturnKnownLocalizedValues(string key, string locale, string expected)
    {
        var sut = new LocalizationService();
        Assert.Equal(expected, sut.Get(key, locale));
    }

    [Theory]
    [InlineData("en")]
    [InlineData("uk")]
    [InlineData("es")]
    [InlineData("fr")]
    [InlineData("de")]
    [InlineData("pl")]
    public void Get_ShouldReturnStubForWipKeys(string locale)
    {
        var sut = new LocalizationService();
        var value = sut.Get("stub.wip", locale);
        Assert.False(string.IsNullOrWhiteSpace(value));
    }

    [Fact]
    public void Get_ShouldReturnFrenchUi_WhenFrenchLocaleSelected()
    {
        var sut = new LocalizationService();
        var value = sut.Get("settings.change_language", "fr");
        Assert.Contains("Changer", value, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("es", "ayud")]
    [InlineData("de", "helfen")]
    [InlineData("pl", "pom")]
    public void Get_ShouldReturnLocalizedMainTitle_WhenLocaleSelected(string locale, string expectedFragment)
    {
        var sut = new LocalizationService();
        var value = sut.Get("menu.main.title", locale);

        Assert.Contains(expectedFragment, value, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(sut.Get("menu.main.title", "en"), value);
    }

    [Theory]
    [InlineData("es", "Cambiar")]
    [InlineData("de", "Sprache")]
    [InlineData("pl", "Zmie")]
    public void Get_ShouldReturnLocalizedUi_WhenLocaleSelected(string locale, string expectedFragment)
    {
        var sut = new LocalizationService();
        var value = sut.Get("settings.change_language", locale);

        Assert.Contains(expectedFragment, value, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(sut.Get("settings.change_language", "en"), value);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("uk")]
    [InlineData("es")]
    [InlineData("fr")]
    [InlineData("de")]
    [InlineData("pl")]
    public void Get_ShouldReturnSafePlaceholder_WhenMissingInAllLocales(string locale)
    {
        var sut = new LocalizationService();
        var key = "nonexistent.key";
        var value = sut.Get(key, locale);
        Assert.Equal("[?:nonexistent.key]", value);
    }

    [Theory]
    [InlineData("es")]
    [InlineData("de")]
    [InlineData("pl")]
    public void Get_ShouldNotReturnEmpty_ForKnownKeysInSupportedLocales(string locale)
    {
        var sut = new LocalizationService();
        var value = sut.Get("menu.main.chat", locale);
        Assert.False(string.IsNullOrWhiteSpace(value));
    }

    [Theory]
    [InlineData("Ukrainian")]
    [InlineData("Spanish")]
    [InlineData("French")]
    [InlineData("German")]
    [InlineData("Polish")]
    public void LocaleDictionaries_ShouldContainAllEnglishKeys(string dictionaryName)
    {
        var english = GetDictionary("English");
        var target = GetDictionary(dictionaryName);
        var missingKeys = english.Keys.Except(target.Keys).ToArray();

        Assert.Empty(missingKeys);
    }

    [Theory]
    [InlineData("en", "menu.weekly.favourites")]
    [InlineData("en", "menu.weekly.log")]
    [InlineData("en", "menu.shopping.add")]
    [InlineData("en", "menu.shopping.list")]
    [InlineData("en", "menu.shopping.delete")]
    [InlineData("uk", "menu.weekly.favourites")]
    [InlineData("uk", "menu.weekly.log")]
    [InlineData("es", "menu.weekly.favourites")]
    [InlineData("de", "menu.weekly.favourites")]
    [InlineData("pl", "menu.weekly.favourites")]
    [InlineData("fr", "menu.weekly.favourites")]
    public void Get_ShouldReturnNonEmptyValue_ForFoodUiKeys(string locale, string key)
    {
        var sut = new LocalizationService();
        var value = sut.Get(key, locale);
        Assert.False(string.IsNullOrWhiteSpace(value));
        Assert.NotEqual(key, value); // not falling back to the raw key
    }

    private static IReadOnlyDictionary<string, string> GetDictionary(string fieldName)
    {
        var field = typeof(LocalizationService).GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(field);

        var value = field!.GetValue(null);
        Assert.IsAssignableFrom<IReadOnlyDictionary<string, string>>(value);

        return (IReadOnlyDictionary<string, string>)value!;
    }
}
