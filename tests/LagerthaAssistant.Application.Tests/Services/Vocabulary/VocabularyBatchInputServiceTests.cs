namespace LagerthaAssistant.Application.Tests.Services.Vocabulary;

using LagerthaAssistant.Application.Services.Vocabulary;
using Xunit;

public sealed class VocabularyBatchInputServiceTests
{
    [Fact]
    public void Parse_ShouldReturnParserItems_WhenNoSpaceSplitScenario()
    {
        var sut = new VocabularyBatchInputService();

        var result = sut.Parse("void; prepare; call back");

        Assert.Equal(["void", "prepare", "call back"], result.Items);
        Assert.False(result.ShouldOfferSpaceSplit);
        Assert.Empty(result.SpaceSplitCandidates);
        Assert.Null(result.SingleItemWithoutSeparators);
    }

    [Fact]
    public void Parse_ShouldOfferSpaceSplit_ForSingleItemWithoutSeparators()
    {
        var sut = new VocabularyBatchInputService();

        var result = sut.Parse("void prepare");

        Assert.Equal(["void prepare"], result.Items);
        Assert.True(result.ShouldOfferSpaceSplit);
        Assert.Equal(["void", "prepare"], result.SpaceSplitCandidates);
        Assert.Equal("void prepare", result.SingleItemWithoutSeparators);
    }

    [Fact]
    public void Parse_ShouldApplySpaceSplit_WhenRequested()
    {
        var sut = new VocabularyBatchInputService();

        var result = sut.Parse("void prepare", applySpaceSplitForSingleItem: true);

        Assert.Equal(["void", "prepare"], result.Items);
        Assert.True(result.ShouldOfferSpaceSplit);
        Assert.Equal(["void", "prepare"], result.SpaceSplitCandidates);
        Assert.Equal("void prepare", result.SingleItemWithoutSeparators);
    }

    [Fact]
    public void Parse_ShouldNotOfferSpaceSplit_WhenSentencePunctuationExists()
    {
        var sut = new VocabularyBatchInputService();

        var result = sut.Parse("The server timed out. We rolled back.");

        Assert.False(result.ShouldOfferSpaceSplit);
        Assert.Empty(result.SpaceSplitCandidates);
    }
}
