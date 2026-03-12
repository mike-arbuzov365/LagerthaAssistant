namespace LagerthaAssistant.Application.Tests.Services.Vocabulary;

using LagerthaAssistant.Application.Services.Vocabulary;
using Xunit;

public sealed class VocabularyBatchInputParserTests
{
    [Fact]
    public void Parse_ShouldSplitMultiLineInput_ByLine()
    {
        const string raw = "void\nprepare\ncall back";

        var items = VocabularyBatchInputParser.Parse(raw);

        Assert.Equal(["void", "prepare", "call back"], items);
    }

    [Fact]
    public void Parse_ShouldSplitSingleLineSentences_BySentenceBoundary()
    {
        const string raw = "The server timed out. We rolled back deployment. The fix is in progress.";

        var items = VocabularyBatchInputParser.Parse(raw);

        Assert.Equal(
            [
                "The server timed out.",
                "We rolled back deployment.",
                "The fix is in progress."
            ],
            items);
    }

    [Fact]
    public void Parse_ShouldSplitSingleLine_BySemicolon()
    {
        const string raw = "void; prepare; call back";

        var items = VocabularyBatchInputParser.Parse(raw);

        Assert.Equal(["void", "prepare", "call back"], items);
    }

    [Fact]
    public void Parse_ShouldSplitSingleLine_ByComma_WhenLooksLikeShortList()
    {
        const string raw = "void, prepare, call back";

        var items = VocabularyBatchInputParser.Parse(raw);

        Assert.Equal(["void", "prepare", "call back"], items);
    }

    [Fact]
    public void Parse_ShouldStripListPrefixes_AndDeduplicate()
    {
        const string raw = "1) void\n2) prepare\n- void\n* call back";

        var items = VocabularyBatchInputParser.Parse(raw);

        Assert.Equal(["void", "prepare", "call back"], items);
    }
}
