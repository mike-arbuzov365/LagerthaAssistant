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
    private const string Marker = "\uD83D\uDD39";

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
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.StartsWith($"--------------------\n{Marker} void", normalized, StringComparison.Ordinal);
        Assert.Contains($"{Marker} void", text, StringComparison.Ordinal);
        Assert.Contains($"{Marker} prepare", text, StringComparison.Ordinal);
        Assert.Contains($"{Marker} void\n\nvoid answer", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--------------------", text);
        Assert.DoesNotContain("\n\n--------------------\n\n", normalized, StringComparison.Ordinal);
        Assert.Contains($"void answer\n--------------------\n{Marker} prepare", normalized, StringComparison.OrdinalIgnoreCase);
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

        Assert.DoesNotContain($"{Marker} cancel\ncancel", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain($"{Marker} celebrate\ncelebrate", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"{Marker} cancel\n\n(v) stop or revoke", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"{Marker} celebrate\n\n(v) honor an event", text, StringComparison.OrdinalIgnoreCase);
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
