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
        var sut = CreateSut(session, sync);

        var result = await sut.HandleAsync(new ConversationAgentContext(ConversationSlashCommands.Help, [ConversationSlashCommands.Help]));

        Assert.Equal("command.help", result.Intent);
        Assert.Contains($"{ConversationCommandCategories.General}:", result.Message);
        Assert.Contains($"{ConversationCommandCategories.Conversation}:", result.Message);
        Assert.Contains($"{ConversationCommandCategories.SystemPrompt}:", result.Message);
        Assert.Contains($"{ConversationCommandCategories.VocabularyIndex}:", result.Message);
        Assert.Contains($"{ConversationCommandCategories.SyncQueue}:", result.Message);
        Assert.Contains($"{ConversationCommandCategories.Session}:", result.Message);
        Assert.Contains($"- {ConversationSlashCommands.Help}", result.Message);
        Assert.Contains($"- {ConversationSlashCommands.PromptSet} <text>", result.Message);
        Assert.Contains($"- {ConversationSlashCommands.Index}", result.Message);
        Assert.Contains($"- {ConversationSlashCommands.IndexClear}", result.Message);
        Assert.Contains($"- {ConversationSlashCommands.IndexRebuild}", result.Message);
        Assert.Contains($"- {ConversationSlashCommands.SyncRun} <n>", result.Message);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnIndexHelp_ForIndexCommand()
    {
        var session = new FakeAssistantSessionService();
        var sync = new FakeVocabularySyncProcessor();
        var sut = CreateSut(session, sync);

        var result = await sut.HandleAsync(new ConversationAgentContext(ConversationSlashCommands.Index, [ConversationSlashCommands.Index]));

        Assert.Equal("command.index.help", result.Intent);
        Assert.Contains(ConversationSlashCommands.IndexClear, result.Message);
        Assert.Contains(ConversationSlashCommands.IndexRebuild, result.Message);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnSyncStatus_ForSyncCommand()
    {
        var session = new FakeAssistantSessionService();
        var sync = new FakeVocabularySyncProcessor { PendingCount = 7 };
        var sut = CreateSut(session, sync);

        var context = new ConversationAgentContext("/sync", ["/sync"]);

        var result = await sut.HandleAsync(context);

        Assert.Equal("command.sync.status", result.Intent);
        Assert.Equal("Pending vocabulary sync jobs: 7.", result.Message);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnFailedSyncList_ForSyncFailedCommand()
    {
        var session = new FakeAssistantSessionService();
        var sync = new FakeVocabularySyncProcessor
        {
            FailedJobs =
            [
                new VocabularySyncFailedJob(
                    42,
                    "void",
                    "wm-nouns-ua-en.xlsx",
                    "local",
                    8,
                    "Retry limit reached",
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow.AddMinutes(-5))
            ]
        };

        var sut = CreateSut(session, sync);
        var result = await sut.HandleAsync(new ConversationAgentContext("/sync failed", ["/sync failed"]));

        Assert.Equal("command.sync.failed", result.Intent);
        Assert.Contains("#42", result.Message);
        Assert.Contains("void", result.Message);
        Assert.Equal(ConversationCommandDefaults.SyncFailedPreviewTake, sync.LastFailedTake);
    }

    [Fact]
    public async Task HandleAsync_ShouldRequeueFailedSyncJobs_ForSyncRetryFailedCommand()
    {
        var session = new FakeAssistantSessionService();
        var sync = new FakeVocabularySyncProcessor
        {
            RequeueResult = 3
        };

        var sut = CreateSut(session, sync);
        var result = await sut.HandleAsync(new ConversationAgentContext("/sync retry failed 9", ["/sync retry failed 9"]));

        Assert.Equal("command.sync.retry-failed", result.Intent);
        Assert.Equal("Requeued failed vocabulary sync jobs: 3.", result.Message);
        Assert.Equal(9, sync.LastRequeueTake);
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
        var sut = CreateSut(session, sync);

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
        var sut = CreateSut(session, sync);

        var result = await sut.HandleAsync(new ConversationAgentContext("/prompt history", ["/prompt history"]));

        Assert.Equal("command.prompt.history", result.Intent);
        Assert.Contains("v2 [active] source=manual created=2026-03-12 10:00:00 UTC", result.Message);
        Assert.Contains("latest prompt", result.Message);
    }

    [Fact]
    public async Task HandleAsync_ShouldSetPrompt_ForPromptSetCommand()
    {
        var session = new FakeAssistantSessionService();
        var sync = new FakeVocabularySyncProcessor();
        var sut = CreateSut(session, sync);

        var result = await sut.HandleAsync(new ConversationAgentContext("/prompt set Keep replies concise", ["/prompt set Keep replies concise"]));

        Assert.Equal("command.prompt.set", result.Intent);
        Assert.Equal("Keep replies concise", session.Prompt);
        Assert.Equal("manual", session.LastSetPromptSource);
        Assert.Contains("System prompt updated and saved.", result.Message);
        Assert.Contains("Keep replies concise", result.Message);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnUnsupported_ForUnknownSlashCommand()
    {
        var session = new FakeAssistantSessionService();
        var sync = new FakeVocabularySyncProcessor();
        var sut = CreateSut(session, sync);

        var context = new ConversationAgentContext("/exit", ["/exit"]);

        var result = await sut.HandleAsync(context);

        Assert.Equal("command.unsupported", result.Intent);
        Assert.Contains("Unsupported command", result.Message);
    }

    [Fact]
    public void CanHandle_ShouldReturnFalse_ForChatMarkerInput()
    {
        var session = new FakeAssistantSessionService();
        var sync = new FakeVocabularySyncProcessor();
        var sut = CreateSut(session, sync);

        var context = new ConversationAgentContext(
            $"{ConversationInputMarkers.Chat} Що ти вмієш?",
            [$"{ConversationInputMarkers.Chat} Що ти вмієш?"]);

        var canHandle = sut.CanHandle(context);

        Assert.False(canHandle);
    }

    private static CommandConversationAgent CreateSut(
        FakeAssistantSessionService session,
        FakeVocabularySyncProcessor sync)
    {
        return new CommandConversationAgent(
            new ConversationIntentRouter(),
            new ConversationCommandCatalogService(),
            session,
            sync);
    }

    private sealed class FakeVocabularySyncProcessor : IVocabularySyncProcessor
    {
        public int PendingCount { get; set; }

        public IReadOnlyList<VocabularySyncFailedJob> FailedJobs { get; set; } = [];

        public int RequeueResult { get; set; }

        public int LastFailedTake { get; private set; }

        public int LastRequeueTake { get; private set; }

        public Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(PendingCount);

        public Task<VocabularySyncRunSummary> ProcessPendingAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult(new VocabularySyncRunSummary(0, 0, 0, 0, 0, 0));

        public Task<IReadOnlyList<VocabularySyncFailedJob>> GetFailedJobsAsync(int take, CancellationToken cancellationToken = default)
        {
            LastFailedTake = take;
            return Task.FromResult(FailedJobs);
        }

        public Task<int> RequeueFailedAsync(int take, CancellationToken cancellationToken = default)
        {
            LastRequeueTake = take;
            return Task.FromResult(RequeueResult);
        }
    }

    private sealed class FakeAssistantSessionService : IAssistantSessionService
    {
        public IReadOnlyCollection<ConversationMessage> Messages => [];

        public IReadOnlyCollection<ConversationMessage> History { get; set; } = [];

        public IReadOnlyCollection<UserMemoryEntry> Memory { get; set; } = [];

        public IReadOnlyCollection<SystemPromptEntry> PromptHistory { get; set; } = [];

        public string Prompt { get; set; } = "default prompt";

        public string? LastSetPromptSource { get; private set; }

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
