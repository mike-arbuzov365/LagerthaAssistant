namespace LagerthaAssistant.IntegrationTests.Services;

using System.Net;
using System.Text;
using System.Text.Json;
using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.Infrastructure.Options;
using LagerthaAssistant.Infrastructure.Services.Vocabulary;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public sealed class GraphAuthServiceTests
{
    [Fact]
    public async Task GetStatusAsync_ShouldReportNotConfigured_WhenClientIdIsMissing()
    {
        var sut = new GraphAuthService(
            new GraphOptions
            {
                ClientId = string.Empty,
                TenantId = "common"
            },
            NullLogger<GraphAuthService>.Instance);

        var status = await sut.GetStatusAsync();

        Assert.False(status.IsConfigured);
        Assert.False(status.IsAuthenticated);
        Assert.Contains("not configured", status.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoginAsync_WithDeviceCodeCallback_ShouldReportNotConfigured_WhenClientIdIsMissing()
    {
        var sut = new GraphAuthService(
            new GraphOptions
            {
                ClientId = string.Empty,
                TenantId = "common"
            },
            NullLogger<GraphAuthService>.Instance);

        var callbackInvoked = false;
        var result = await sut.LoginAsync(
            (_, _) =>
            {
                callbackInvoked = true;
                return Task.CompletedTask;
            });

        Assert.False(result.Succeeded);
        Assert.Contains("not configured", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(callbackInvoked);
    }

    [Fact]
    public async Task StartLoginAsync_ShouldReportNotConfigured_WhenClientIdIsMissing()
    {
        var sut = new GraphAuthService(
            new GraphOptions
            {
                ClientId = string.Empty,
                TenantId = "common"
            },
            NullLogger<GraphAuthService>.Instance);

        var result = await sut.StartLoginAsync();

        Assert.False(result.Succeeded);
        Assert.Contains("not configured", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(result.Challenge);
    }

    [Fact]
    public async Task CompleteLoginAsync_ShouldReportNotConfigured_WhenClientIdIsMissing()
    {
        var sut = new GraphAuthService(
            new GraphOptions
            {
                ClientId = string.Empty,
                TenantId = "common"
            },
            NullLogger<GraphAuthService>.Instance);

        var result = await sut.CompleteLoginAsync(
            new GraphDeviceLoginChallenge(
                DeviceCode: "device-code",
                UserCode: "ABCD-EFGH",
                VerificationUri: "https://www.microsoft.com/link",
                ExpiresInSeconds: 900,
                IntervalSeconds: 5,
                ExpiresAtUtc: DateTimeOffset.UtcNow.AddMinutes(10),
                Message: null));

        Assert.False(result.Succeeded);
        Assert.Contains("not configured", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetStatusAsync_ShouldUseRefreshToken_WhenAccessTokenExpired()
    {
        var tokenPath = CreateTempTokenCachePath();
        var expiredEntry = new
        {
            AccessToken = "expired-token",
            RefreshToken = "refresh-token",
            AccessTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5)
        };
        await File.WriteAllTextAsync(tokenPath, JsonSerializer.Serialize(expiredEntry));

        var handler = new StubHttpMessageHandler(async request =>
        {
            var body = await request.Content!.ReadAsStringAsync();
            Assert.Contains("grant_type=refresh_token", body, StringComparison.Ordinal);
            Assert.Contains("refresh_token=refresh-token", body, StringComparison.Ordinal);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"access_token\":\"fresh-token\",\"refresh_token\":\"fresh-refresh\",\"expires_in\":3600}",
                    Encoding.UTF8,
                    "application/json")
            };
        });

        var sut = new GraphAuthService(
            new GraphOptions
            {
                ClientId = "client-id",
                TenantId = "common",
                TokenCachePath = tokenPath
            },
            NullLogger<GraphAuthService>.Instance,
            new HttpClient(handler));

        var status = await sut.GetStatusAsync();

        Assert.True(status.IsConfigured);
        Assert.True(status.IsAuthenticated);
        Assert.Contains("Authenticated", status.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, handler.CallCount);

        var updatedCache = await File.ReadAllTextAsync(tokenPath);
        Assert.Contains("fresh-token", updatedCache, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetStatusAsync_ShouldReportExpired_WhenRefreshFails()
    {
        var tokenPath = CreateTempTokenCachePath();
        var expiredEntry = new
        {
            AccessToken = "expired-token",
            RefreshToken = "refresh-token",
            AccessTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5)
        };
        await File.WriteAllTextAsync(tokenPath, JsonSerializer.Serialize(expiredEntry));

        var handler = new StubHttpMessageHandler(_ =>
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(
                    "{\"error\":\"invalid_grant\",\"error_description\":\"expired\"}",
                    Encoding.UTF8,
                    "application/json")
            });
        });

        var sut = new GraphAuthService(
            new GraphOptions
            {
                ClientId = "client-id",
                TenantId = "common",
                TokenCachePath = tokenPath
            },
            NullLogger<GraphAuthService>.Instance,
            new HttpClient(handler));

        var status = await sut.GetStatusAsync();

        Assert.True(status.IsConfigured);
        Assert.False(status.IsAuthenticated);
        Assert.Contains("expired", status.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GetStatusAsync_ShouldNotCallRefresh_WhenAccessTokenStillValid()
    {
        var tokenPath = CreateTempTokenCachePath();
        var validEntry = new
        {
            AccessToken = "valid-token",
            RefreshToken = "refresh-token",
            AccessTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(30)
        };
        await File.WriteAllTextAsync(tokenPath, JsonSerializer.Serialize(validEntry));

        var handler = new StubHttpMessageHandler(_ =>
        {
            throw new InvalidOperationException("Refresh endpoint should not be called when token is still valid.");
        });

        var sut = new GraphAuthService(
            new GraphOptions
            {
                ClientId = "client-id",
                TenantId = "common",
                TokenCachePath = tokenPath
            },
            NullLogger<GraphAuthService>.Instance,
            new HttpClient(handler));

        var status = await sut.GetStatusAsync();

        Assert.True(status.IsConfigured);
        Assert.True(status.IsAuthenticated);
        Assert.Equal(0, handler.CallCount);
    }

    private static string CreateTempTokenCachePath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "LagerthaAssistant", "graph-auth-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "graph-token.json");
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _responder = responder;

        public int CallCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return await _responder(request);
        }
    }
}
