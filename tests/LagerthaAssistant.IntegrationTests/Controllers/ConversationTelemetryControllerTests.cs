namespace LagerthaAssistant.IntegrationTests.Controllers;

using LagerthaAssistant.Api.Controllers;
using LagerthaAssistant.Api.Contracts;
using LagerthaAssistant.Application.Interfaces.Agents;
using LagerthaAssistant.Application.Models.Agents;
using Microsoft.AspNetCore.Mvc;
using Xunit;

public sealed class ConversationTelemetryControllerTests
{
    [Fact]
    public async Task GetIntents_ShouldReturnBadRequest_WhenDaysOutOfRange()
    {
        var service = new FakeConversationMetricsService();
        var sut = new ConversationTelemetryController(service);

        var result = await sut.GetIntents(days: 0, top: 20, channel: null, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetIntents_ShouldReturnBadRequest_WhenTopOutOfRange()
    {
        var service = new FakeConversationMetricsService();
        var sut = new ConversationTelemetryController(service);

        var result = await sut.GetIntents(days: 7, top: 201, channel: null, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetIntents_ShouldReturnMetrics()
    {
        var service = new FakeConversationMetricsService
        {
            Rows =
            [
                new ConversationIntentMetricSummary("api", "command-agent", "command.history", false, 4, 4, new DateTimeOffset(2026, 03, 12, 10, 0, 0, TimeSpan.Zero))
            ]
        };

        var sut = new ConversationTelemetryController(service);

        var result = await sut.GetIntents(days: 7, top: 20, channel: "API", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<ConversationIntentMetricsResponse>(ok.Value);

        Assert.Equal(7, payload.Days);
        Assert.Equal(20, payload.Top);
        Assert.Equal("api", payload.Channel);
        var first = Assert.Single(payload.Items);
        Assert.Equal("command.history", first.Intent);
        Assert.Equal(4, first.Count);
    }

    private sealed class FakeConversationMetricsService : IConversationMetricsService
    {
        public IReadOnlyList<ConversationIntentMetricSummary> Rows { get; set; } = [];

        public Task TrackAsync(string channel, ConversationAgentResult result, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<ConversationIntentMetricSummary>> GetTopIntentsAsync(int days, int take, string? channel = null, CancellationToken cancellationToken = default)
            => Task.FromResult(Rows);
    }
}
