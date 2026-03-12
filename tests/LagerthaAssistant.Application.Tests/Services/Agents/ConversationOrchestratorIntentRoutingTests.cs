namespace LagerthaAssistant.Application.Tests.Services.Agents;

using LagerthaAssistant.Application.Interfaces.AI;
using LagerthaAssistant.Application.Interfaces.Agents;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.AI;
using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.Application.Services.Agents;
using LagerthaAssistant.Domain.AI;
using LagerthaAssistant.Domain.Entities;
using Xunit;

public sealed class ConversationOrchestratorIntentRoutingTests
{
    [Fact]
    public async Task ProcessAsync_ShouldRouteNaturalCommand_ToCommandAgent()
    {
        var workflow = new FakeVocabularyWorkflowService();
        var session = new FakeAssistantSessionService
        {
            History = [ConversationMessage.Create(MessageRole.User, "void", DateTimeOffset.UtcNow)]
        };

        var sync = new FakeVocabularySyncProcessor();

        IConversationAgent[] agents =
        [
            new CommandConversationAgent(new ConversationIntentRouter(), session, sync),
            new VocabularyConversationAgent(workflow)
        ];

        var sut = new ConversationOrchestrator(agents);

        var result = await sut.ProcessAsync("show conversation history");

        Assert.Equal("command-agent", result.AgentName);
        Assert.Equal("command.history", result.Intent);
        Assert.Equal(0, workflow.SingleCalls);
        Assert.Equal(0, workflow.BatchCalls);
    }

    [Fact]
    public async Task ProcessAsync_ShouldRouteSingleWord_ToVocabularyAgent()
    {
        var workflow = new FakeVocabularyWorkflowService();
        var session = new FakeAssistantSessionService();
        var sync = new FakeVocabularySyncProcessor();

        IConversationAgent[] agents =
        [
            new CommandConversationAgent(new ConversationIntentRouter(), session, sync),
            new VocabularyConversationAgent(workflow)
        ];

        var sut = new ConversationOrchestrator(agents);

        var result = await sut.ProcessAsync("history");

        Assert.Equal("vocabulary-agent", result.AgentName);
        Assert.Equal("vocabulary.single", result.Intent);
        Assert.Equal(1, workflow.SingleCalls);
        Assert.Equal(0, workflow.BatchCalls);
    }

    private sealed class FakeVocabularyWorkflowService : IVocabularyWorkflowService
    {
        public int SingleCalls { get; private set; }

        public int BatchCalls { get; private set; }

        public Task<VocabularyWorkflowItemResult> ProcessAsync(
            string input,
            string? forcedDeckFileName = null,
            string? overridePartOfSpeech = null,
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
            return Task.FromResult<IReadOnlyList<VocabularyWorkflowItemResult>>(inputs.Select(BuildSingle).ToList());
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
    }

    private sealed class FakeVocabularySyncProcessor : IVocabularySyncProcessor
    {
        public Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<VocabularySyncRunSummary> ProcessPendingAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult(new VocabularySyncRunSummary(0, 0, 0, 0, 0, 0));
    }

    private sealed class FakeAssistantSessionService : IAssistantSessionService
    {
        public IReadOnlyCollection<ConversationMessage> Messages => [];

        public IReadOnlyCollection<ConversationMessage> History { get; set; } = [];

        public Task<AssistantCompletionResult> AskAsync(string userMessage, CancellationToken cancellationToken = default)
            => Task.FromResult(new AssistantCompletionResult("reply", "test-model", null));

        public Task<IReadOnlyCollection<ConversationMessage>> GetRecentHistoryAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult(History);

        public Task<IReadOnlyCollection<UserMemoryEntry>> GetActiveMemoryAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<UserMemoryEntry>>([]);

        public Task<string> GetSystemPromptAsync(CancellationToken cancellationToken = default)
            => Task.FromResult("prompt");

        public Task<IReadOnlyCollection<SystemPromptEntry>> GetSystemPromptHistoryAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<SystemPromptEntry>>([]);

        public Task<IReadOnlyCollection<SystemPromptProposal>> GetSystemPromptProposalsAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<SystemPromptProposal>>([]);

        public Task<SystemPromptProposal> CreateSystemPromptProposalAsync(string prompt, string reason, double confidence, string source = "manual", CancellationToken cancellationToken = default)
            => Task.FromResult(new SystemPromptProposal());

        public Task<SystemPromptProposal> GenerateSystemPromptProposalAsync(string goal, CancellationToken cancellationToken = default)
            => Task.FromResult(new SystemPromptProposal());

        public Task<string> ApplySystemPromptProposalAsync(int proposalId, CancellationToken cancellationToken = default)
            => Task.FromResult("prompt");

        public Task RejectSystemPromptProposalAsync(int proposalId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<string> SetSystemPromptAsync(string prompt, string source = "manual", CancellationToken cancellationToken = default)
            => Task.FromResult(prompt);

        public void Reset()
        {
        }
    }
}
