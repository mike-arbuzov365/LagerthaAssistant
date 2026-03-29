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
    [InlineData("fr", LocalizationConstants.EnglishLocale)]
    [InlineData("de", LocalizationConstants.EnglishLocale)]
    public void GetLocaleForUser_ShouldMapOnlyToEnOrUk(string? languageCode, string expected)
    {
        var sut = new LocalizationService();

        var locale = sut.GetLocaleForUser(languageCode);

        Assert.Equal(expected, locale);
    }

    [Fact]
    public void Get_ShouldReturnEnglishFallback_ForUnsupportedLocale()
    {
        var sut = new LocalizationService();

        var value = sut.Get("menu.main.title", "fr");

        Assert.Equal("What can I help you with?", value);
    }

    [Fact]
    public void Get_ShouldReturnUkrainianValue_ForUkrainianLocale()
    {
        var sut = new LocalizationService();

        var value = sut.Get("menu.main.title", "uk");

        Assert.Contains("допомог", value, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Get_ShouldReturnPlaceholder_WhenKeyMissingEverywhere()
    {
        var sut = new LocalizationService();

        var value = sut.Get("nonexistent.key", "uk");

        Assert.Equal("[?:nonexistent.key]", value);
    }
}
