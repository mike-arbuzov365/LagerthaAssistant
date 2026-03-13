namespace LagerthaAssistant.IntegrationTests.Controllers;

using LagerthaAssistant.Api.Contracts;
using LagerthaAssistant.Api.Controllers;
using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.Interfaces.AI;
using LagerthaAssistant.Application.Interfaces.Agents;
using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Models.AI;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Domain.AI;
using LagerthaAssistant.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Xunit;

public sealed class ConversationControllerTests
{
    [Fact]
    public void GetCommands_ShouldReturnSlashCommandCatalog()
    {
        var orchestrator = new FakeConversationOrchestrator();
        var sessionService = new FakeAssistantSessionService();
        var scopeAccessor = new FakeConversationScopeAccessor();
        var sut = new ConversationController(orchestrator, sessionService, scopeAccessor);

        var response = sut.GetCommands();

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsAssignableFrom<IReadOnlyList<ConversationCommandItemResponse>>(ok.Value);

        Assert.NotEmpty(payload);
        Assert.All(payload, item => Assert.False(string.IsNullOrWhiteSpace(item.Category)));
        Assert.Contains(payload, item => item.Command == ConversationSlashCommands.Help && item.Category == ConversationCommandCategories.General);
        Assert.Contains(payload, item => item.Command == $"{ConversationSlashCommands.PromptSet} <text>" && item.Category == ConversationCommandCategories.SystemPrompt);
        Assert.Contains(payload, item => item.Command == $"{ConversationSlashCommands.SyncRun} <n>" && item.Category == ConversationCommandCategories.SyncQueue);
    }

    [Fact]
    public void GetGroupedCommands_ShouldReturnGroupedCatalog()
    {
        var orchestrator = new FakeConversationOrchestrator();
        var sessionService = new FakeAssistantSessionService();
        var scopeAccessor = new FakeConversationScopeAccessor();
        var sut = new ConversationController(orchestrator, sessionService, scopeAccessor);

        var response = sut.GetGroupedCommands();

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsAssignableFrom<IReadOnlyList<ConversationCommandGroupResponse>>(ok.Value);

        Assert.NotEmpty(payload);

        var generalGroup = Assert.Single(payload, group => group.Category == ConversationCommandCategories.General);
        Assert.Contains(generalGroup.Commands, item => item.Command == ConversationSlashCommands.Help);

        var syncGroup = Assert.Single(payload, group => group.Category == ConversationCommandCategories.SyncQueue);
        Assert.Contains(syncGroup.Commands, item => item.Command == ConversationSlashCommands.SyncRun);
    }

    [Fact]
    public async Task GetHistory_ShouldSetScopeAndReturnMappedHistory()
    {
        var orchestrator = new FakeConversationOrchestrator();
        var sessionService = new FakeAssistantSessionService
        {
            History =
            [
                ConversationMessage.Create(MessageRole.User, "hello", DateTimeOffset.UtcNow)
            ]
        };
        var scopeAccessor = new FakeConversationScopeAccessor();
        var sut = new ConversationController(orchestrator, sessionService, scopeAccessor);

        var response = await sut.GetHistory(10, "  TeLeGrAm  ", "Mike", "chat-42", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsAssignableFrom<IReadOnlyList<ConversationHistoryEntryResponse>>(ok.Value);

        var entry = Assert.Single(payload);
        Assert.Equal("user", entry.Role);
        Assert.Equal("hello", entry.Content);

        Assert.Equal("telegram", scopeAccessor.Current.Channel);
        Assert.Equal("mike", scopeAccessor.Current.UserId);
        Assert.Equal("chat-42", scopeAccessor.Current.ConversationId);
    }

    [Fact]
    public async Task GetMemory_ShouldUseDefaults_WhenScopeNotProvided()
    {
        var orchestrator = new FakeConversationOrchestrator();
        var sessionService = new FakeAssistantSessionService
        {
            Memory =
            [
                new UserMemoryEntry
                {
                    Key = MemoryKeys.UserName,
                    Value = "Mike",
                    Confidence = 0.95,
                    IsActive = true,
                    LastSeenAtUtc = DateTimeOffset.UtcNow
                }
            ]
        };
        var scopeAccessor = new FakeConversationScopeAccessor();
        var sut = new ConversationController(orchestrator, sessionService, scopeAccessor);

        var response = await sut.GetMemory(5, null, null, null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsAssignableFrom<IReadOnlyList<ConversationMemoryEntryResponse>>(ok.Value);

        var entry = Assert.Single(payload);
        Assert.Equal(MemoryKeys.UserName, entry.Key);
        Assert.Equal("Mike", entry.Value);

        Assert.Equal("api", scopeAccessor.Current.Channel);
        Assert.Equal("anonymous", scopeAccessor.Current.UserId);
        Assert.Equal("default", scopeAccessor.Current.ConversationId);
    }

    [Fact]
    public async Task PostMessage_ShouldUseDefaultChannel_WhenNotProvided()
    {
        var orchestrator = new FakeConversationOrchestrator();
        var sessionService = new FakeAssistantSessionService();
        var scopeAccessor = new FakeConversationScopeAccessor();
        var sut = new ConversationController(orchestrator, sessionService, scopeAccessor);

        var response = await sut.PostMessage(new ConversationMessageRequest("void"), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<ConversationMessageResponse>(ok.Value);

        Assert.Equal("api", orchestrator.LastChannel);
        Assert.Equal("vocabulary.single", payload.Intent);
    }

    [Fact]
    public async Task PostMessage_ShouldNormalizeProvidedChannel()
    {
        var orchestrator = new FakeConversationOrchestrator();
        var sessionService = new FakeAssistantSessionService();
        var scopeAccessor = new FakeConversationScopeAccessor();
        var sut = new ConversationController(orchestrator, sessionService, scopeAccessor);

        var response = await sut.PostMessage(new ConversationMessageRequest("void", "  TeLeGrAm  "), CancellationToken.None);

        Assert.IsType<OkObjectResult>(response.Result);
        Assert.Equal("telegram", orchestrator.LastChannel);
    }

    [Fact]
    public async Task PostMessage_ShouldForwardUserAndConversationIds()
    {
        var orchestrator = new FakeConversationOrchestrator();
        var sessionService = new FakeAssistantSessionService();
        var scopeAccessor = new FakeConversationScopeAccessor();
        var sut = new ConversationController(orchestrator, sessionService, scopeAccessor);

        var response = await sut.PostMessage(
            new ConversationMessageRequest("void", "api", "Mike", "chat-42"),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(response.Result);
        Assert.Equal("Mike", orchestrator.LastUserId);
        Assert.Equal("chat-42", orchestrator.LastConversationId);
    }

    [Fact]
    public async Task PostMessage_ShouldMapCommandResultPayload()
    {
        var orchestrator = new FakeConversationOrchestrator
        {
            NextResult = ConversationAgentResult.Empty("command-agent", "command.prompt.set", "System prompt updated and saved.")
        };

        var sessionService = new FakeAssistantSessionService();
        var scopeAccessor = new FakeConversationScopeAccessor();
        var sut = new ConversationController(orchestrator, sessionService, scopeAccessor);

        var response = await sut.PostMessage(new ConversationMessageRequest("/prompt set Keep replies concise"), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<ConversationMessageResponse>(ok.Value);

        Assert.Equal("command-agent", payload.Agent);
        Assert.Equal("command.prompt.set", payload.Intent);
        Assert.False(payload.IsBatch);
        Assert.Empty(payload.Items);
        Assert.Equal("System prompt updated and saved.", payload.Message);
    }

    [Fact]
    public async Task PostMessage_ShouldReturnBadRequest_WhenInputIsEmpty()
    {
        var orchestrator = new FakeConversationOrchestrator();
        var sessionService = new FakeAssistantSessionService();
        var scopeAccessor = new FakeConversationScopeAccessor();
        var sut = new ConversationController(orchestrator, sessionService, scopeAccessor);

        var response = await sut.PostMessage(new ConversationMessageRequest("   "), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(response.Result);
        Assert.Equal(0, orchestrator.Calls);
    }

    private sealed class FakeConversationOrchestrator : IConversationOrchestrator
    {
        public int Calls { get; private set; }

        public string LastChannel { get; private set; } = string.Empty;

        public string? LastUserId { get; private set; }

        public string? LastConversationId { get; private set; }

        public ConversationAgentResult NextResult { get; set; } = new(
            "vocabulary-agent",
            "vocabulary.single",
            false,
            []);

        public Task<ConversationAgentResult> ProcessAsync(string input, CancellationToken cancellationToken = default)
            => ProcessAsync(input, "unknown", null, null, cancellationToken);

        public Task<ConversationAgentResult> ProcessAsync(
            string input,
            string channel,
            CancellationToken cancellationToken = default)
            => ProcessAsync(input, channel, null, null, cancellationToken);

        public Task<ConversationAgentResult> ProcessAsync(
            string input,
            string channel,
            string? userId,
            string? conversationId,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            LastChannel = channel;
            LastUserId = userId;
            LastConversationId = conversationId;
            return Task.FromResult(NextResult);
        }
    }

    private sealed class FakeConversationScopeAccessor : IConversationScopeAccessor
    {
        public ConversationScope Current { get; private set; } = ConversationScope.Default;

        public void Set(ConversationScope scope)
        {
            Current = scope;
        }
    }

    private sealed class FakeAssistantSessionService : IAssistantSessionService
    {
        public IReadOnlyCollection<ConversationMessage> Messages => [];

        public IReadOnlyCollection<ConversationMessage> History { get; set; } = [];

        public IReadOnlyCollection<UserMemoryEntry> Memory { get; set; } = [];

        public Task<AssistantCompletionResult> AskAsync(string userMessage, CancellationToken cancellationToken = default)
            => Task.FromResult(new AssistantCompletionResult("ok", "test-model", null));

        public Task<IReadOnlyCollection<ConversationMessage>> GetRecentHistoryAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult(History);

        public Task<IReadOnlyCollection<UserMemoryEntry>> GetActiveMemoryAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult(Memory);

        public Task<string> GetSystemPromptAsync(CancellationToken cancellationToken = default)
            => Task.FromResult("prompt");

        public Task<IReadOnlyCollection<SystemPromptEntry>> GetSystemPromptHistoryAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<SystemPromptEntry>>([]);

        public Task<IReadOnlyCollection<SystemPromptProposal>> GetSystemPromptProposalsAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<SystemPromptProposal>>([]);

        public Task<SystemPromptProposal> CreateSystemPromptProposalAsync(
            string prompt,
            string reason,
            double confidence,
            string source = "manual",
            CancellationToken cancellationToken = default)
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

