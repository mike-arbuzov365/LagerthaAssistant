namespace LagerthaAssistant.IntegrationTests.Services;

using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Infrastructure.Services;
using Xunit;

public sealed class LocalizationServiceTests
{
    [Theory]
    [InlineData("ru", LocalizationConstants.UkrainianLocale)]
    [InlineData("ru-RU", LocalizationConstants.UkrainianLocale)]
    [InlineData("uk", LocalizationConstants.UkrainianLocale)]
    [InlineData("en", LocalizationConstants.EnglishLocale)]
    [InlineData(null, LocalizationConstants.EnglishLocale)]
    public void GetLocaleForUser_ShouldApplyExpectedMapping(string? languageCode, string expected)
    {
        var sut = new LocalizationService();

        var locale = sut.GetLocaleForUser(languageCode);

        Assert.Equal(expected, locale);
    }
}
