namespace LagerthaAssistant.Application.Tests.Services.Agents;

using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.AI;
using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Services.Agents;
using Xunit;

public sealed class VocabularyConversationAgentTests
{
    [Fact]
    public async Task HandleAsync_ShouldProcessBatch_WhenContextContainsMultipleItems()
    {
        var workflow = new FakeVocabularyWorkflowService();
        var sut = new VocabularyConversationAgent(workflow);

        var context = new ConversationAgentContext(
            "void; prepare",
            ["void", "prepare"]);

        var result = await sut.HandleAsync(context);

        Assert.Equal("vocabulary.batch", result.Intent);
        Assert.True(result.IsBatch);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(0, workflow.SingleCalls);
        Assert.Equal(1, workflow.BatchCalls);
    }

    [Fact]
    public async Task HandleAsync_ShouldProcessSingle_WhenContextContainsOneItem()
    {
        var workflow = new FakeVocabularyWorkflowService();
        var sut = new VocabularyConversationAgent(workflow);

        var context = new ConversationAgentContext("void", ["void"]);

        var result = await sut.HandleAsync(context);

        Assert.Equal("vocabulary.single", result.Intent);
        Assert.False(result.IsBatch);
        Assert.Single(result.Items);
        Assert.Equal(1, workflow.SingleCalls);
        Assert.Equal(0, workflow.BatchCalls);
    }

    private static VocabularyWorkflowItemResult BuildSingle(string input)
    {
        var lookup = new VocabularyLookupResult(input, []);
        var completion = new AssistantCompletionResult($"{input}\n\n(n) test\n\nExample.", "test-model", null);
        var preview = new VocabularyAppendPreviewResult(
            VocabularyAppendPreviewStatus.ReadyToAppend,
            input,
            "wm-nouns-ua-en.xlsx",
            "C:/deck/wm-nouns-ua-en.xlsx");

        return new VocabularyWorkflowItemResult(input, lookup, completion, preview);
    }

    private sealed class FakeVocabularyWorkflowService : IVocabularyWorkflowService
    {
        public int SingleCalls { get; private set; }

        public int BatchCalls { get; private set; }

        public Task<VocabularyWorkflowItemResult> ProcessAsync(
            string input,
            string? forcedDeckFileName = null,
            string? overridePartOfSpeech = null,
            bool bypassValidation = false,
            CancellationToken cancellationToken = default)
        {
            SingleCalls++;
            return Task.FromResult(BuildSingle(input));
        }

        public Task<IReadOnlyList<VocabularyWorkflowItemResult>> ProcessBatchAsync(
            IReadOnlyList<string> inputs,
            CancellationToken cancellationToken = default)
        {
            BatchCalls++;
            var output = inputs.Select(BuildSingle).ToList();
            return Task.FromResult<IReadOnlyList<VocabularyWorkflowItemResult>>(output);
        }
    }
}

