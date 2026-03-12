namespace LagerthaAssistant.Application.Tests.Services.Agents;

using LagerthaAssistant.Application.Constants;
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
    public async Task HandleAsync_ShouldReturnCatalogBackedHelp_ForHelpCommand()
    {
        var session = new FakeAssistantSessionService();
        var sync = new FakeVocabularySyncProcessor();
        var sut = new CommandConversationAgent(new ConversationIntentRouter(), session, sync);

        var result = await sut.HandleAsync(new ConversationAgentContext(ConversationSlashCommands.Help, [ConversationSlashCommands.Help]));

        Assert.Equal("command.help", result.Intent);
        Assert.Contains($"- {ConversationSlashCommands.Help}", result.Message);
        Assert.Contains($"- {ConversationSlashCommands.PromptSet} <text>", result.Message);
        Assert.Contains($"- {ConversationSlashCommands.SyncRun} <n>", result.Message);
    }

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
                ConversationMessage.Create(MessageRole.Assistant, "(n) emptiness", DateTimeOffset.UtcNow)
            ]
        };

        var sync = new FakeVocabularySyncProcessor();
        var sut = new CommandConversationAgent(new ConversationIntentRouter(), session, sync);

        var context = new ConversationAgentContext("show conversation history", ["show conversation history"]);

        var result = await sut.HandleAsync(context);

        Assert.Equal("command.history", result.Intent);
        Assert.Contains("- User: void", result.Message);
        Assert.Contains("- Assistant: (n) emptiness", result.Message);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnPromptHistory_ForPromptHistoryCommand()
    {
        var timestamp = new DateTimeOffset(2026, 03, 12, 10, 0, 0, TimeSpan.Zero);
        var session = new FakeAssistantSessionService
        {
            PromptHistory =
            [
                new SystemPromptEntry
                {
                    Version = 2,
                    IsActive = true,
                    Source = "manual",
                    CreatedAtUtc = timestamp,
                    PromptText = "latest prompt"
                }
            ]
        };

        var sync = new FakeVocabularySyncProcessor();
        var sut = new CommandConversationAgent(new ConversationIntentRouter(), session, sync);

        var result = await sut.HandleAsync(new ConversationAgentContext("/prompt history", ["/prompt history"]));

        Assert.Equal("command.prompt.history", result.Intent);
        Assert.Contains("v2 [active] source=manual created=2026-03-12 10:00:00 UTC", result.Message);
        Assert.Contains("latest prompt", result.Message);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnPromptProposals_ForPromptProposalsCommand()
    {
        var timestamp = new DateTimeOffset(2026, 03, 12, 11, 30, 0, TimeSpan.Zero);
        var session = new FakeAssistantSessionService
        {
            PromptProposals =
            [
                new SystemPromptProposal
                {
                    Id = 42,
                    Status = "proposed",
                    Source = "ai",
                    Confidence = 0.95,
                    CreatedAtUtc = timestamp,
                    Reason = "Improve formatting consistency",
                    ProposedPrompt = "new prompt"
                }
            ]
        };

        var sync = new FakeVocabularySyncProcessor();
        var sut = new CommandConversationAgent(new ConversationIntentRouter(), session, sync);

        var result = await sut.HandleAsync(new ConversationAgentContext("/prompt proposals", ["/prompt proposals"]));

        Assert.Equal("command.prompt.proposals", result.Intent);
        Assert.Contains("#42 status=proposed source=ai confidence=0.95 created=2026-03-12 11:30:00 UTC", result.Message);
        Assert.Contains("reason: Improve formatting consistency", result.Message);
        Assert.Contains("prompt: new prompt", result.Message);
    }

    [Fact]
    public async Task HandleAsync_ShouldSetPrompt_ForPromptSetCommand()
    {
        var session = new FakeAssistantSessionService();
        var sync = new FakeVocabularySyncProcessor();
        var sut = new CommandConversationAgent(new ConversationIntentRouter(), session, sync);

        var result = await sut.HandleAsync(new ConversationAgentContext("/prompt set Keep replies concise", ["/prompt set Keep replies concise"]));

        Assert.Equal("command.prompt.set", result.Intent);
        Assert.Equal("Keep replies concise", session.Prompt);
        Assert.Equal("manual", session.LastSetPromptSource);
        Assert.Contains("System prompt updated and saved.", result.Message);
        Assert.Contains("Keep replies concise", result.Message);
    }

    [Fact]
    public async Task HandleAsync_ShouldCreateProposal_ForPromptProposeCommand()
    {
        var session = new FakeAssistantSessionService();
        var sync = new FakeVocabularySyncProcessor();
        var sut = new CommandConversationAgent(new ConversationIntentRouter(), session, sync);

        var result = await sut.HandleAsync(new ConversationAgentContext("/prompt propose Too verbose || Keep replies concise", ["/prompt propose Too verbose || Keep replies concise"]));

        Assert.Equal("command.prompt.propose", result.Intent);
        Assert.Equal("Too verbose", session.LastProposalReason);
        Assert.Equal("Keep replies concise", session.LastProposalPrompt);
        Assert.Contains("Proposal #101", result.Message);
    }

    [Fact]
    public async Task HandleAsync_ShouldGenerateProposal_ForPromptImproveCommand()
    {
        var session = new FakeAssistantSessionService();
        var sync = new FakeVocabularySyncProcessor();
        var sut = new CommandConversationAgent(new ConversationIntentRouter(), session, sync);

        var result = await sut.HandleAsync(new ConversationAgentContext("/prompt improve improve vocabulary examples", ["/prompt improve improve vocabulary examples"]));

        Assert.Equal("command.prompt.improve", result.Intent);
        Assert.Equal("improve vocabulary examples", session.LastGeneratedGoal);
        Assert.Contains("AI proposal #202 generated", result.Message);
    }

    [Fact]
    public async Task HandleAsync_ShouldApplyProposal_ForPromptApplyCommand()
    {
        var session = new FakeAssistantSessionService { Prompt = "applied prompt" };
        var sync = new FakeVocabularySyncProcessor();
        var sut = new CommandConversationAgent(new ConversationIntentRouter(), session, sync);

        var result = await sut.HandleAsync(new ConversationAgentContext("/prompt apply 12", ["/prompt apply 12"]));

        Assert.Equal("command.prompt.apply", result.Intent);
        Assert.Equal(12, session.LastAppliedProposalId);
        Assert.Contains("Proposal #12 applied.", result.Message);
        Assert.Contains("applied prompt", result.Message);
    }

    [Fact]
    public async Task HandleAsync_ShouldRejectProposal_ForPromptRejectCommand()
    {
        var session = new FakeAssistantSessionService();
        var sync = new FakeVocabularySyncProcessor();
        var sut = new CommandConversationAgent(new ConversationIntentRouter(), session, sync);

        var result = await sut.HandleAsync(new ConversationAgentContext("/prompt reject 6", ["/prompt reject 6"]));

        Assert.Equal("command.prompt.reject", result.Intent);
        Assert.Equal(6, session.LastRejectedProposalId);
        Assert.Equal("Proposal #6 rejected.", result.Message);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnUsage_ForPromptApplyWithoutId()
    {
        var session = new FakeAssistantSessionService();
        var sync = new FakeVocabularySyncProcessor();
        var sut = new CommandConversationAgent(new ConversationIntentRouter(), session, sync);

        var result = await sut.HandleAsync(new ConversationAgentContext("/prompt apply", ["/prompt apply"]));

        Assert.Equal("command.prompt.apply", result.Intent);
        Assert.Equal("Usage: /prompt apply <proposalId>", result.Message);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnUsage_ForPromptRejectWithInvalidId()
    {
        var session = new FakeAssistantSessionService();
        var sync = new FakeVocabularySyncProcessor();
        var sut = new CommandConversationAgent(new ConversationIntentRouter(), session, sync);

        var result = await sut.HandleAsync(new ConversationAgentContext("/prompt reject xyz", ["/prompt reject xyz"]));

        Assert.Equal("command.prompt.reject", result.Intent);
        Assert.Equal("Usage: /prompt reject <proposalId>", result.Message);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnUsage_ForPromptProposeWithoutPayload()
    {
        var session = new FakeAssistantSessionService();
        var sync = new FakeVocabularySyncProcessor();
        var sut = new CommandConversationAgent(new ConversationIntentRouter(), session, sync);

        var result = await sut.HandleAsync(new ConversationAgentContext("/prompt propose", ["/prompt propose"]));

        Assert.Equal("command.prompt.propose", result.Intent);
        Assert.Equal("Usage: /prompt propose <reason> || <new prompt text>", result.Message);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnUsage_ForPromptImproveWithoutGoal()
    {
        var session = new FakeAssistantSessionService();
        var sync = new FakeVocabularySyncProcessor();
        var sut = new CommandConversationAgent(new ConversationIntentRouter(), session, sync);

        var result = await sut.HandleAsync(new ConversationAgentContext("/prompt improve", ["/prompt improve"]));

        Assert.Equal("command.prompt.improve", result.Intent);
        Assert.Equal("Usage: /prompt improve <goal>", result.Message);
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

        public IReadOnlyCollection<SystemPromptEntry> PromptHistory { get; set; } = [];

        public IReadOnlyCollection<SystemPromptProposal> PromptProposals { get; set; } = [];

        public string Prompt { get; set; } = "default prompt";

        public string? LastSetPromptSource { get; private set; }

        public string? LastProposalPrompt { get; private set; }

        public string? LastProposalReason { get; private set; }

        public string? LastGeneratedGoal { get; private set; }

        public int? LastAppliedProposalId { get; private set; }

        public int? LastRejectedProposalId { get; private set; }

        public Task<AssistantCompletionResult> AskAsync(string userMessage, CancellationToken cancellationToken = default)
            => Task.FromResult(new AssistantCompletionResult("reply", "test-model", null));

        public Task<IReadOnlyCollection<ConversationMessage>> GetRecentHistoryAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult(History);

        public Task<IReadOnlyCollection<UserMemoryEntry>> GetActiveMemoryAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult(Memory);

        public Task<string> GetSystemPromptAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Prompt);

        public Task<IReadOnlyCollection<SystemPromptEntry>> GetSystemPromptHistoryAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult(PromptHistory);

        public Task<IReadOnlyCollection<SystemPromptProposal>> GetSystemPromptProposalsAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult(PromptProposals);

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
        {
            LastGeneratedGoal = goal;
            return Task.FromResult(new SystemPromptProposal
            {
                Id = 202,
                Status = "proposed"
            });
        }

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
            LastSetPromptSource = source;
            return Task.FromResult(prompt);
        }

        public void Reset()
        {
        }
    }
}