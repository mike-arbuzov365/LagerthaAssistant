namespace LagerthaAssistant.Application.Tests.Services.Vocabulary;

using LagerthaAssistant.Application.Services.Vocabulary;
using Xunit;

public sealed class VocabularyDeckMarkerSuggesterTests
{
    [Theory]
    [InlineData("wm-nouns-ua-en.xlsx", "n")]
    [InlineData("wm-verbs-us-en.xlsx", "v")]
    [InlineData("wm-irregular-verbs-ua-en.xlsx", "iv")]
    [InlineData("wm-phrasal-verbs-ua-en.xlsx", "pv")]
    [InlineData("wm-adjectives-ua-en.xlsx", "adj")]
    [InlineData("wm-adverbs-ua-en.xlsx", "adv")]
    [InlineData("wm-prepositions-ua-en.xlsx", "prep")]
    [InlineData("wm-conjunctions-ua-en.xlsx", "conj")]
    [InlineData("wm-pronouns-ua-en.xlsx", "pron")]
    [InlineData("wm-persistant-expressions-ua-en.xlsx", "pe")]
    [InlineData("wm-persistent-expressions-ua-en.xlsx", "pe")]
    public void SuggestMarker_ShouldReturnExpectedMarker(string fileName, string expectedMarker)
    {
        var result = VocabularyDeckMarkerSuggester.SuggestMarker(fileName);

        Assert.Equal(expectedMarker, result);
    }

    [Fact]
    public void SuggestMarker_ShouldReturnNull_WhenFileNameIsUnknown()
    {
        var result = VocabularyDeckMarkerSuggester.SuggestMarker("wm-custom-deck.xlsx");

        Assert.Null(result);
    }

    [Fact]
    public void SuggestMarker_ShouldReturnNull_WhenFileNameIsEmpty()
    {
        var result = VocabularyDeckMarkerSuggester.SuggestMarker(" ");

        Assert.Null(result);
    }
}
