namespace LagerthaAssistant.IntegrationTests.Services;

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
}
