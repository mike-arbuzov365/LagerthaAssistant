namespace LagerthaAssistant.IntegrationTests.Services;

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
    [InlineData("ru", true)]
    [InlineData("ru-RU", true)]
    [InlineData("uk", false)]
    [InlineData("en", false)]
    [InlineData(null, false)]
    public void IsRussian_ShouldDetectExpectedCodes(string? languageCode, bool expected)
    {
        var sut = new LocalizationService();
        Assert.Equal(expected, sut.IsRussian(languageCode));
    }

    [Theory]
    [InlineData("menu.main.chat", "en", "🗣 Chat")]
    [InlineData("menu.main.chat", "uk", "🗣 Чат")]
    [InlineData("menu.main.vocabulary", "en", "📚 Vocabulary")]
    [InlineData("menu.main.vocabulary", "uk", "📚 Словник")]
    public void Get_ShouldReturnKnownLocalizedValues(string key, string locale, string expected)
    {
        var sut = new LocalizationService();
        Assert.Equal(expected, sut.Get(key, locale));
    }

    [Theory]
    [InlineData("en")]
    [InlineData("uk")]
    public void Get_ShouldReturnStubForWipKeys(string locale)
    {
        var sut = new LocalizationService();
        var value = sut.Get("stub.wip", locale);
        Assert.False(string.IsNullOrWhiteSpace(value));
        Assert.StartsWith("🚧", value, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("uk")]
    public void Get_ShouldFallbackToEmptyString_WhenMissing(string locale)
    {
        var sut = new LocalizationService();
        var key = "nonexistent.key";
        var value = sut.Get(key, locale);
        Assert.Equal(string.Empty, value);
    }
}
