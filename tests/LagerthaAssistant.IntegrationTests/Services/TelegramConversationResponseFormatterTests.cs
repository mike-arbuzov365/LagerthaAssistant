namespace LagerthaAssistant.IntegrationTests.Services;

using LagerthaAssistant.Api.Services;
using LagerthaAssistant.Application.Models.AI;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Models.Vocabulary;
using Xunit;

public sealed class TelegramConversationResponseFormatterTests
{
    [Fact]
    public void Format_ShouldPreferTopLevelMessage_WhenPresent()
    {
        var sut = new TelegramConversationResponseFormatter();
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
        var sut = new TelegramConversationResponseFormatter();
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
        var sut = new TelegramConversationResponseFormatter();
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
}
