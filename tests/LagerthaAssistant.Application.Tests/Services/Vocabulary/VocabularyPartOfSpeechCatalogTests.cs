namespace LagerthaAssistant.Application.Tests.Services.Vocabulary;

using LagerthaAssistant.Application.Services.Vocabulary;
using Xunit;

public sealed class VocabularyPartOfSpeechCatalogTests
{
    [Fact]
    public void GetOptions_ShouldReturnOrderedStableCatalog()
    {
        var options = VocabularyPartOfSpeechCatalog.GetOptions();

        Assert.Equal(10, options.Count);
        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8, 9, 10], options.Select(option => option.Number));
        Assert.Equal(["n", "v", "iv", "pv", "adj", "adv", "prep", "conj", "pron", "pe"], options.Select(option => option.Marker));
    }

    [Theory]
    [InlineData("n", "n")]
    [InlineData("noun", "n")]
    [InlineData("1", "n")]
    [InlineData("verb", "v")]
    [InlineData("2", "v")]
    [InlineData("irregular", "iv")]
    [InlineData("3", "iv")]
    [InlineData("phrasal-verb", "pv")]
    [InlineData("4", "pv")]
    [InlineData("adjective", "adj")]
    [InlineData("5", "adj")]
    [InlineData("adverb", "adv")]
    [InlineData("6", "adv")]
    [InlineData("preposition", "prep")]
    [InlineData("7", "prep")]
    [InlineData("conjunction", "conj")]
    [InlineData("8", "conj")]
    [InlineData("pronoun", "pron")]
    [InlineData("9", "pron")]
    [InlineData("persistent-expression", "pe")]
    [InlineData("persistant-expression", "pe")]
    [InlineData("10", "pe")]
    public void TryNormalize_ShouldSupportAliasesAndNumbers(string raw, string expected)
    {
        var result = VocabularyPartOfSpeechCatalog.TryNormalize(raw, out var marker);

        Assert.True(result);
        Assert.Equal(expected, marker);
    }

    [Fact]
    public void NormalizeOrNull_ShouldReturnNull_ForUnknownMarker()
    {
        var result = VocabularyPartOfSpeechCatalog.NormalizeOrNull("something-else");

        Assert.Null(result);
    }
}
