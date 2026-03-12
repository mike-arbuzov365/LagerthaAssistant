namespace LagerthaAssistant.IntegrationTests.Controllers;

using LagerthaAssistant.Api.Contracts;
using LagerthaAssistant.Api.Controllers;
using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.Interfaces.Agents;
using LagerthaAssistant.Application.Models.Agents;
using Microsoft.AspNetCore.Mvc;
using Xunit;

public sealed class ConversationControllerTests
{
    [Fact]
    public void GetCommands_ShouldReturnSlashCommandCatalog()
    {
        var orchestrator = new FakeConversationOrchestrator();
        var sut = new ConversationController(orchestrator);

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
        var sut = new ConversationController(orchestrator);

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
    public async Task PostMessage_ShouldUseDefaultChannel_WhenNotProvided()
    {
        var orchestrator = new FakeConversationOrchestrator();
        var sut = new ConversationController(orchestrator);

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
        var sut = new ConversationController(orchestrator);

        var response = await sut.PostMessage(new ConversationMessageRequest("void", "  TeLeGrAm  "), CancellationToken.None);

        Assert.IsType<OkObjectResult>(response.Result);
        Assert.Equal("telegram", orchestrator.LastChannel);
    }

    [Fact]
    public async Task PostMessage_ShouldForwardUserAndConversationIds()
    {
        var orchestrator = new FakeConversationOrchestrator();
        var sut = new ConversationController(orchestrator);

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

        var sut = new ConversationController(orchestrator);

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
        var sut = new ConversationController(orchestrator);

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
}
