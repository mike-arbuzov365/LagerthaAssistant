namespace LagerthaAssistant.Application.Tests.Services.Agents;

using LagerthaAssistant.Application.Interfaces.AI;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.AI;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.Application.Services.Agents;
using LagerthaAssistant.Domain.AI;
using LagerthaAssistant.Domain.Entities;
using Xunit;

public sealed class CommandConversationAgentTests
{
    [Fact]
    public async Task HandleAsync_ShouldReturnSyncStatus_ForSyncCommand()
    {
        var session = new FakeAssistantSessionService();
        var sync = new FakeVocabularySyncProcessor { PendingCount = 7 };
        var sut = new CommandConversationAgent(new ConversationIntentRouter(), session, sync);

        var context = new ConversationAgentContext("/sync", ["/sync"]);

        var result = await sut.HandleAsync(context);

        Assert.Equal("command.sync.status", result.Intent);
        Assert.Equal("Pending vocabulary sync jobs: 7.", result.Message);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnHistory_ForNaturalIntent()
    {
        var session = new FakeAssistantSessionService
        {
            History =
            [
                ConversationMessage.Create(MessageRole.User, "void", DateTimeOffset.UtcNow),
                ConversationMessage.Create(MessageRole.Assistant, "(n) порожнеча", DateTimeOffset.UtcNow)
            ]
        };

        var sync = new FakeVocabularySyncProcessor();
        var sut = new CommandConversationAgent(new ConversationIntentRouter(), session, sync);

        var context = new ConversationAgentContext("show conversation history", ["show conversation history"]);

        var result = await sut.HandleAsync(context);

        Assert.Equal("command.history", result.Intent);
        Assert.Contains("- User: void", result.Message);
        Assert.Contains("- Assistant: (n) порожнеча", result.Message);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnUnsupported_ForUnknownSlashCommand()
    {
        var session = new FakeAssistantSessionService();
        var sync = new FakeVocabularySyncProcessor();
        var sut = new CommandConversationAgent(new ConversationIntentRouter(), session, sync);

        var context = new ConversationAgentContext("/exit", ["/exit"]);

        var result = await sut.HandleAsync(context);

        Assert.Equal("command.unsupported", result.Intent);
        Assert.Contains("Unsupported command", result.Message);
    }

    private sealed class FakeVocabularySyncProcessor : IVocabularySyncProcessor
    {
        public int PendingCount { get; set; }

        public Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(PendingCount);

        public Task<VocabularySyncRunSummary> ProcessPendingAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult(new VocabularySyncRunSummary(0, 0, 0, 0, 0, 0));
    }

    private sealed class FakeAssistantSessionService : IAssistantSessionService
    {
        public IReadOnlyCollection<ConversationMessage> Messages => [];

        public IReadOnlyCollection<ConversationMessage> History { get; set; } = [];

        public IReadOnlyCollection<UserMemoryEntry> Memory { get; set; } = [];

        public string Prompt { get; set; } = "default prompt";

        public Task<AssistantCompletionResult> AskAsync(string userMessage, CancellationToken cancellationToken = default)
            => Task.FromResult(new AssistantCompletionResult("reply", "test-model", null));

        public Task<IReadOnlyCollection<ConversationMessage>> GetRecentHistoryAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult(History);

        public Task<IReadOnlyCollection<UserMemoryEntry>> GetActiveMemoryAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult(Memory);

        public Task<string> GetSystemPromptAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Prompt);

        public Task<IReadOnlyCollection<SystemPromptEntry>> GetSystemPromptHistoryAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<SystemPromptEntry>>([]);

        public Task<IReadOnlyCollection<SystemPromptProposal>> GetSystemPromptProposalsAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<SystemPromptProposal>>([]);

        public Task<SystemPromptProposal> CreateSystemPromptProposalAsync(string prompt, string reason, double confidence, string source = "manual", CancellationToken cancellationToken = default)
            => Task.FromResult(new SystemPromptProposal());

        public Task<SystemPromptProposal> GenerateSystemPromptProposalAsync(string goal, CancellationToken cancellationToken = default)
            => Task.FromResult(new SystemPromptProposal());

        public Task<string> ApplySystemPromptProposalAsync(int proposalId, CancellationToken cancellationToken = default)
            => Task.FromResult(Prompt);

        public Task RejectSystemPromptProposalAsync(int proposalId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<string> SetSystemPromptAsync(string prompt, string source = "manual", CancellationToken cancellationToken = default)
        {
            Prompt = prompt;
            return Task.FromResult(prompt);
        }

        public void Reset()
        {
        }
    }
}
