namespace LagerthaAssistant.IntegrationTests.Services;

using LagerthaAssistant.Api.Options;
using LagerthaAssistant.Api.Services;
using LagerthaAssistant.Application.Models.AI;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Models.Vocabulary;
using Microsoft.Extensions.Options;
using Xunit;

public sealed class TelegramConversationResponseFormatterTests
{
    private static TelegramConversationResponseFormatter CreateSut(int textLengthLimit = 3900)
        => new(Options.Create(new TelegramOptions { TextLengthLimit = textLengthLimit }));

    [Fact]
    public void Format_ShouldPreferTopLevelMessage_WhenPresent()
    {
        var sut = CreateSut();
        var result = new ConversationAgentResult(
            AgentName: "command-agent",
            Intent: "command.help",
            IsBatch: false,
            Items: [],
            Message: "Help text");

        var text = sut.Format(result);

        Assert.Equal("Help text", text);
    }

    [Fact]
    public void Format_ShouldRenderAssistantCompletion_ForSingleItem()
    {
        var sut = CreateSut();
        var item = new ConversationAgentItemResult(
            Input: "void",
            Lookup: new VocabularyLookupResult("void", []),
            AssistantCompletion: new AssistantCompletionResult("void\n\n(n) empty", "gpt-4.1-mini", null),
            AppendPreview: null);

        var result = new ConversationAgentResult("vocabulary-agent", "vocabulary.single", false, [item]);

        var text = sut.Format(result);

        Assert.Contains("(n) empty", text);
    }

    [Fact]
    public void Format_ShouldEnumerateBatchItems()
    {
        var sut = CreateSut();
        var first = new ConversationAgentItemResult(
            Input: "void",
            Lookup: new VocabularyLookupResult("void", []),
            AssistantCompletion: new AssistantCompletionResult("void answer", "gpt-4.1-mini", null),
            AppendPreview: null);
        var second = new ConversationAgentItemResult(
            Input: "prepare",
            Lookup: new VocabularyLookupResult("prepare", []),
            AssistantCompletion: new AssistantCompletionResult("prepare answer", "gpt-4.1-mini", null),
            AppendPreview: null);

        var result = new ConversationAgentResult("vocabulary-agent", "vocabulary.batch", true, [first, second]);

        var text = sut.Format(result);

        Assert.Contains("1) void", text);
        Assert.Contains("2) prepare", text);
    }

    [Fact]
    public void Format_ShouldNotDuplicateInputLine_InBatchItemBody()
    {
        var sut = CreateSut();
        var first = new ConversationAgentItemResult(
            Input: "cancel",
            Lookup: new VocabularyLookupResult("cancel", []),
            AssistantCompletion: new AssistantCompletionResult(
                "cancel\n\n(v) stop or revoke",
                "gpt-4.1-mini",
                null),
            AppendPreview: null);
        var second = new ConversationAgentItemResult(
            Input: "celebrate",
            Lookup: new VocabularyLookupResult("celebrate", []),
            AssistantCompletion: new AssistantCompletionResult(
                "celebrate\n\n(v) honor an event",
                "gpt-4.1-mini",
                null),
            AppendPreview: null);

        var result = new ConversationAgentResult("vocabulary-agent", "vocabulary.batch", true, [first, second]);

        var text = sut.Format(result);

        Assert.DoesNotContain("1) cancel\ncancel", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("2) celebrate\ncelebrate", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("1) cancel\n(v) stop or revoke", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2) celebrate\n(v) honor an event", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Format_ShouldTruncateText_WhenExceedsCustomLimit()
    {
        var limit = 50;
        var sut = CreateSut(textLengthLimit: limit);
        var longText = new string('x', 200);
        var item = new ConversationAgentItemResult(
            Input: "void",
            Lookup: new VocabularyLookupResult("void", []),
            AssistantCompletion: new AssistantCompletionResult(longText, "gpt-4.1-mini", null),
            AppendPreview: null);

        var result = new ConversationAgentResult("vocabulary-agent", "vocabulary.single", false, [item]);

        var text = sut.Format(result);

        Assert.True(text.Length <= limit, $"Expected text length <= {limit}, got {text.Length}");
    }
}
