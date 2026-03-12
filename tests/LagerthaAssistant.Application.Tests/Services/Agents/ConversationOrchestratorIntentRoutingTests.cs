namespace LagerthaAssistant.Application.Tests.Services.Agents;

using LagerthaAssistant.Application.Interfaces.AI;
using LagerthaAssistant.Application.Interfaces.Agents;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.AI;
using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.Application.Services.Agents;
using LagerthaAssistant.Domain.AI;
using LagerthaAssistant.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
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

        var sut = new ConversationOrchestrator(agents, NullLogger<ConversationOrchestrator>.Instance);

        var result = await sut.ProcessAsync("show conversation history");

        Assert.Equal("command-agent", result.AgentName);
        Assert.Equal("command.history", result.Intent);
        Assert.Equal(0, workflow.SingleCalls);
        Assert.Equal(0, workflow.BatchCalls);
    }

    [Fact]
    public async Task ProcessAsync_ShouldRouteSlashPromptSet_ToCommandAgent()
    {
        var workflow = new FakeVocabularyWorkflowService();
        var session = new FakeAssistantSessionService();
        var sync = new FakeVocabularySyncProcessor();

        IConversationAgent[] agents =
        [
            new CommandConversationAgent(new ConversationIntentRouter(), session, sync),
            new VocabularyConversationAgent(workflow)
        ];

        var sut = new ConversationOrchestrator(agents, NullLogger<ConversationOrchestrator>.Instance);

        var result = await sut.ProcessAsync("/prompt set Keep replies concise");

        Assert.Equal("command-agent", result.AgentName);
        Assert.Equal("command.prompt.set", result.Intent);
        Assert.Equal("Keep replies concise", session.LastSetPrompt);
        Assert.Equal(0, workflow.SingleCalls);
        Assert.Equal(0, workflow.BatchCalls);
    }

    [Fact]
    public async Task ProcessAsync_ShouldRouteMultilinePromptSet_ToCommandAgent()
    {
        var workflow = new FakeVocabularyWorkflowService();
        var session = new FakeAssistantSessionService();
        var sync = new FakeVocabularySyncProcessor();

        IConversationAgent[] agents =
        [
            new CommandConversationAgent(new ConversationIntentRouter(), session, sync),
            new VocabularyConversationAgent(workflow)
        ];

        var sut = new ConversationOrchestrator(agents, NullLogger<ConversationOrchestrator>.Instance);
        var prompt = $"line one{Environment.NewLine}line two";

        var result = await sut.ProcessAsync($"/prompt set {prompt}");

        Assert.Equal("command-agent", result.AgentName);
        Assert.Equal("command.prompt.set", result.Intent);
        Assert.Equal(prompt, session.LastSetPrompt);
        Assert.Equal(0, workflow.SingleCalls);
        Assert.Equal(0, workflow.BatchCalls);
    }

    [Fact]
    public async Task ProcessAsync_ShouldRoutePromptPropose_ToCommandAgent()
    {
        var workflow = new FakeVocabularyWorkflowService();
        var session = new FakeAssistantSessionService();
        var sync = new FakeVocabularySyncProcessor();

        IConversationAgent[] agents =
        [
            new CommandConversationAgent(new ConversationIntentRouter(), session, sync),
            new VocabularyConversationAgent(workflow)
        ];

        var sut = new ConversationOrchestrator(agents, NullLogger<ConversationOrchestrator>.Instance);

        var result = await sut.ProcessAsync("/prompt propose Too verbose || Keep replies concise");

        Assert.Equal("command-agent", result.AgentName);
        Assert.Equal("command.prompt.propose", result.Intent);
        Assert.Equal("Too verbose", session.LastProposalReason);
        Assert.Equal("Keep replies concise", session.LastProposalPrompt);
        Assert.Equal(0, workflow.SingleCalls);
        Assert.Equal(0, workflow.BatchCalls);
    }

    [Fact]
    public async Task ProcessAsync_ShouldRoutePromptApply_ToCommandAgent()
    {
        var workflow = new FakeVocabularyWorkflowService();
        var session = new FakeAssistantSessionService();
        var sync = new FakeVocabularySyncProcessor();

        IConversationAgent[] agents =
        [
            new CommandConversationAgent(new ConversationIntentRouter(), session, sync),
            new VocabularyConversationAgent(workflow)
        ];

        var sut = new ConversationOrchestrator(agents, NullLogger<ConversationOrchestrator>.Instance);

        var result = await sut.ProcessAsync("/prompt apply 17");

        Assert.Equal("command-agent", result.AgentName);
        Assert.Equal("command.prompt.apply", result.Intent);
        Assert.Equal(17, session.LastAppliedProposalId);
        Assert.Equal(0, workflow.SingleCalls);
        Assert.Equal(0, workflow.BatchCalls);
    }

    [Fact]
    public async Task ProcessAsync_ShouldRoutePromptReject_ToCommandAgent()
    {
        var workflow = new FakeVocabularyWorkflowService();
        var session = new FakeAssistantSessionService();
        var sync = new FakeVocabularySyncProcessor();

        IConversationAgent[] agents =
        [
            new CommandConversationAgent(new ConversationIntentRouter(), session, sync),
            new VocabularyConversationAgent(workflow)
        ];

        var sut = new ConversationOrchestrator(agents, NullLogger<ConversationOrchestrator>.Instance);

        var result = await sut.ProcessAsync("/prompt reject 19");

        Assert.Equal("command-agent", result.AgentName);
        Assert.Equal("command.prompt.reject", result.Intent);
        Assert.Equal(19, session.LastRejectedProposalId);
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

        var sut = new ConversationOrchestrator(agents, NullLogger<ConversationOrchestrator>.Instance);

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

        public string Prompt { get; private set; } = "prompt";

        public string? LastSetPrompt { get; private set; }

        public string? LastProposalPrompt { get; private set; }

        public string? LastProposalReason { get; private set; }

        public int? LastAppliedProposalId { get; private set; }

        public int? LastRejectedProposalId { get; private set; }

        public Task<AssistantCompletionResult> AskAsync(string userMessage, CancellationToken cancellationToken = default)
            => Task.FromResult(new AssistantCompletionResult("reply", "test-model", null));

        public Task<IReadOnlyCollection<ConversationMessage>> GetRecentHistoryAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult(History);

        public Task<IReadOnlyCollection<UserMemoryEntry>> GetActiveMemoryAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<UserMemoryEntry>>([]);

        public Task<string> GetSystemPromptAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Prompt);

        public Task<IReadOnlyCollection<SystemPromptEntry>> GetSystemPromptHistoryAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<SystemPromptEntry>>([]);

        public Task<IReadOnlyCollection<SystemPromptProposal>> GetSystemPromptProposalsAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<SystemPromptProposal>>([]);

        public Task<SystemPromptProposal> CreateSystemPromptProposalAsync(string prompt, string reason, double confidence, string source = "manual", CancellationToken cancellationToken = default)
        {
            LastProposalPrompt = prompt;
            LastProposalReason = reason;
            return Task.FromResult(new SystemPromptProposal
            {
                Id = 101,
                Status = "proposed"
            });
        }

        public Task<SystemPromptProposal> GenerateSystemPromptProposalAsync(string goal, CancellationToken cancellationToken = default)
            => Task.FromResult(new SystemPromptProposal());

        public Task<string> ApplySystemPromptProposalAsync(int proposalId, CancellationToken cancellationToken = default)
        {
            LastAppliedProposalId = proposalId;
            return Task.FromResult(Prompt);
        }

        public Task RejectSystemPromptProposalAsync(int proposalId, CancellationToken cancellationToken = default)
        {
            LastRejectedProposalId = proposalId;
            return Task.CompletedTask;
        }

        public Task<string> SetSystemPromptAsync(string prompt, string source = "manual", CancellationToken cancellationToken = default)
        {
            Prompt = prompt;
            LastSetPrompt = prompt;
            return Task.FromResult(prompt);
        }

        public void Reset()
        {
        }
    }
}