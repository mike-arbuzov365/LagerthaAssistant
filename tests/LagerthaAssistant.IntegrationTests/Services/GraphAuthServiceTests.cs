namespace LagerthaAssistant.IntegrationTests.Services;

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
}
