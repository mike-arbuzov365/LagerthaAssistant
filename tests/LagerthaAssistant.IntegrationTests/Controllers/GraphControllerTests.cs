namespace LagerthaAssistant.IntegrationTests.Controllers;

using LagerthaAssistant.Api.Contracts;
using LagerthaAssistant.Api.Controllers;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Vocabulary;
using Microsoft.AspNetCore.Mvc;
using Xunit;

public sealed class GraphControllerTests
{
    [Fact]
    public async Task GetStatus_ShouldReturnMappedGraphAuthStatus()
    {
        var graphAuth = new FakeGraphAuthService
        {
            NextStatus = new GraphAuthStatus(
                IsConfigured: true,
                IsAuthenticated: true,
                Message: "Authenticated",
                AccessTokenExpiresAtUtc: new DateTimeOffset(2026, 3, 20, 10, 0, 0, TimeSpan.Zero))
        };
        var sut = new GraphController(graphAuth);

        var response = await sut.GetStatus(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<GraphAuthStatusResponse>(ok.Value);

        Assert.True(payload.IsConfigured);
        Assert.True(payload.IsAuthenticated);
        Assert.Equal("Authenticated", payload.Message);
        Assert.Equal(new DateTimeOffset(2026, 3, 20, 10, 0, 0, TimeSpan.Zero), payload.AccessTokenExpiresAtUtc);
    }

    [Fact]
    public async Task Login_ShouldReturnLoginResult_AndFreshStatus()
    {
        var graphAuth = new FakeGraphAuthService
        {
            NextLoginResult = new GraphLoginResult(true, "Graph login completed successfully."),
            StatusSequence = new Queue<GraphAuthStatus>([
                new GraphAuthStatus(true, true, "Authenticated", new DateTimeOffset(2026, 3, 20, 10, 0, 0, TimeSpan.Zero))
            ])
        };
        var sut = new GraphController(graphAuth);

        var response = await sut.Login(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<GraphLoginResponse>(ok.Value);

        Assert.True(payload.Succeeded);
        Assert.Equal("Graph login completed successfully.", payload.Message);
        Assert.True(payload.Status.IsConfigured);
        Assert.True(payload.Status.IsAuthenticated);
        Assert.Equal(1, graphAuth.LoginCalls);
        Assert.Equal(1, graphAuth.StatusCalls);
    }

    [Fact]
    public async Task Logout_ShouldClearToken_AndReturnUpdatedStatus()
    {
        var graphAuth = new FakeGraphAuthService
        {
            NextStatus = new GraphAuthStatus(true, false, "Not authenticated", null)
        };
        var sut = new GraphController(graphAuth);

        var response = await sut.Logout(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<GraphAuthStatusResponse>(ok.Value);

        Assert.True(graphAuth.LogoutCalled);
        Assert.False(payload.IsAuthenticated);
        Assert.Equal("Not authenticated", payload.Message);
    }

    private sealed class FakeGraphAuthService : IGraphAuthService
    {
        public GraphAuthStatus NextStatus { get; set; } = new(false, false, "Not configured", null);

        public Queue<GraphAuthStatus> StatusSequence { get; set; } = [];

        public GraphLoginResult NextLoginResult { get; set; } = new(false, "Not configured.");

        public bool LogoutCalled { get; private set; }

        public int LoginCalls { get; private set; }

        public int StatusCalls { get; private set; }

        public Task<GraphAuthStatus> GetStatusAsync(CancellationToken cancellationToken = default)
        {
            StatusCalls++;

            if (StatusSequence.Count > 0)
            {
                return Task.FromResult(StatusSequence.Dequeue());
            }

            return Task.FromResult(NextStatus);
        }

        public Task<GraphLoginResult> LoginAsync(CancellationToken cancellationToken = default)
        {
            LoginCalls++;
            return Task.FromResult(NextLoginResult);
        }

        public Task<GraphLoginResult> LoginAsync(
            Func<GraphDeviceCodePrompt, CancellationToken, Task> onDeviceCodeReceived,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(onDeviceCodeReceived);
            LoginCalls++;
            return Task.FromResult(NextLoginResult);
        }

        public Task LogoutAsync(CancellationToken cancellationToken = default)
        {
            LogoutCalled = true;
            return Task.CompletedTask;
        }

        public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<string?>(null);
        }
    }
}
