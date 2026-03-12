namespace LagerthaAssistant.Application.Tests.Services.Agents;

using LagerthaAssistant.Application.Interfaces.Agents;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.AI;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.Application.Services.Agents;
using Xunit;

public sealed class ConversationOrchestratorTests
{
    [Fact]
    public async Task ProcessAsync_ShouldUseCommandAgent_ForSlashInput()
    {
        var workflow = new FakeVocabularyWorkflowService();
        var agents = new IConversationAgent[]
        {
            new FakeSlashCommandAgent(),
            new VocabularyConversationAgent(workflow)
        };

        var sut = new ConversationOrchestrator(agents);

        var result = await sut.ProcessAsync("/help");

        Assert.Equal("command-agent", result.AgentName);
        Assert.Equal("command", result.Intent);
        Assert.Empty(result.Items);
        Assert.Equal(0, workflow.SingleCalls);
        Assert.Equal(0, workflow.BatchCalls);
    }

    [Fact]
    public async Task ProcessAsync_ShouldUseVocabularyAgent_ForRegularInput()
    {
        var workflow = new FakeVocabularyWorkflowService
        {
            SingleFactory = input => BuildSingle(input)
        };

        var agents = new IConversationAgent[]
        {
            new FakeSlashCommandAgent(),
            new VocabularyConversationAgent(workflow)
        };

        var sut = new ConversationOrchestrator(agents);

        var result = await sut.ProcessAsync("void");

        Assert.Equal("vocabulary-agent", result.AgentName);
        Assert.Equal("vocabulary.single", result.Intent);
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

    private sealed class FakeSlashCommandAgent : IConversationAgent
    {
        public string Name => "command-agent";

        public int Order => 10;

        public bool CanHandle(ConversationAgentContext context)
            => context.IsSlashCommand;

        public Task<ConversationAgentResult> HandleAsync(ConversationAgentContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(ConversationAgentResult.Empty(Name, "command", "Slash command"));
    }

    private sealed class FakeVocabularyWorkflowService : IVocabularyWorkflowService
    {
        public int SingleCalls { get; private set; }

        public int BatchCalls { get; private set; }

        public Func<string, VocabularyWorkflowItemResult>? SingleFactory { get; set; }

        public Func<IReadOnlyList<string>, IReadOnlyList<VocabularyWorkflowItemResult>>? BatchFactory { get; set; }

        public Task<VocabularyWorkflowItemResult> ProcessAsync(
            string input,
            string? forcedDeckFileName = null,
            string? overridePartOfSpeech = null,
            CancellationToken cancellationToken = default)
        {
            SingleCalls++;
            return Task.FromResult(SingleFactory?.Invoke(input) ?? BuildSingle(input));
        }

        public Task<IReadOnlyList<VocabularyWorkflowItemResult>> ProcessBatchAsync(
            IReadOnlyList<string> inputs,
            CancellationToken cancellationToken = default)
        {
            BatchCalls++;
            var output = BatchFactory?.Invoke(inputs)
                ?? inputs.Select(BuildSingle).ToList();

            return Task.FromResult(output);
        }
    }
}
