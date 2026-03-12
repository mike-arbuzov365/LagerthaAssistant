namespace LagerthaAssistant.IntegrationTests.Controllers;

using LagerthaAssistant.Api.Contracts;
using LagerthaAssistant.Api.Controllers;
using LagerthaAssistant.Application.Interfaces.Agents;
using LagerthaAssistant.Application.Models.Agents;
using Microsoft.AspNetCore.Mvc;
using Xunit;

public sealed class ConversationControllerTests
{
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

        public Task<ConversationAgentResult> ProcessAsync(string input, CancellationToken cancellationToken = default)
            => ProcessAsync(input, "unknown", cancellationToken);

        public Task<ConversationAgentResult> ProcessAsync(
            string input,
            string channel,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            LastChannel = channel;

            return Task.FromResult(new ConversationAgentResult(
                "vocabulary-agent",
                "vocabulary.single",
                false,
                []));
        }
    }
}
