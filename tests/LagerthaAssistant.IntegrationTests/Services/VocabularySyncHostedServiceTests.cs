namespace LagerthaAssistant.IntegrationTests.Services;

using LagerthaAssistant.Api.HostedServices;
using LagerthaAssistant.Api.Options;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Vocabulary;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

public sealed class VocabularySyncHostedServiceTests
{
    [Theory]
    [InlineData(60, 300, 2, 0, 60)]
    [InlineData(60, 300, 2, 1, 120)]
    [InlineData(60, 300, 2, 2, 240)]
    [InlineData(60, 300, 2, 3, 300)]
    [InlineData(10, 120, 3, 2, 90)]
    [InlineData(10, 120, 3, 3, 120)]
    public void CalculateDelay_ShouldApplyExponentialBackoff_WithCap(
        int intervalSeconds,
        int maxBackoffSeconds,
        int backoffFactor,
        int failureStreak,
        int expectedSeconds)
    {
        var delay = VocabularySyncHostedService.CalculateDelay(
            intervalSeconds,
            maxBackoffSeconds,
            backoffFactor,
            failureStreak);

        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), delay);
    }

    [Fact]
    public async Task StartAsync_ShouldRunSyncOnStartup_WhenEnabled()
    {
        var processor = new FakeVocabularySyncProcessor();

        await using var provider = BuildProvider(processor);

        var sut = new VocabularySyncHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new VocabularySyncWorkerOptions
            {
                Enabled = true,
                RunOnStartup = true,
                BatchSize = 7,
                IntervalSeconds = 60
            }),
            NullLogger<VocabularySyncHostedService>.Instance);

        await sut.StartAsync(CancellationToken.None);

        for (var i = 0; i < 20 && processor.Calls == 0; i++)
        {
            await Task.Delay(25);
        }

        await sut.StopAsync(CancellationToken.None);

        Assert.Equal(1, processor.Calls);
        Assert.Equal(7, processor.LastTake);
    }

    [Fact]
    public async Task StartAsync_ShouldNotRunSync_WhenWorkerDisabled()
    {
        var processor = new FakeVocabularySyncProcessor();

        await using var provider = BuildProvider(processor);

        var sut = new VocabularySyncHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new VocabularySyncWorkerOptions
            {
                Enabled = false,
                RunOnStartup = true,
                BatchSize = 7,
                IntervalSeconds = 60
            }),
            NullLogger<VocabularySyncHostedService>.Instance);

        await sut.StartAsync(CancellationToken.None);
        await Task.Delay(50);
        await sut.StopAsync(CancellationToken.None);

        Assert.Equal(0, processor.Calls);
    }

    private static ServiceProvider BuildProvider(FakeVocabularySyncProcessor processor)
    {
        var services = new ServiceCollection();
        services.AddScoped<IVocabularySyncProcessor>(_ => processor);
        return services.BuildServiceProvider();
    }

    private sealed class FakeVocabularySyncProcessor : IVocabularySyncProcessor
    {
        public int Calls { get; private set; }

        public int LastTake { get; private set; }

        public Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<VocabularySyncRunSummary> ProcessPendingAsync(int take, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastTake = take;
            return Task.FromResult(new VocabularySyncRunSummary(
                Requested: 0,
                Processed: 0,
                Completed: 0,
                Requeued: 0,
                Failed: 0,
                PendingAfterRun: 0));
        }

        public Task<IReadOnlyList<VocabularySyncFailedJob>> GetFailedJobsAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<VocabularySyncFailedJob>>([]);

        public Task<int> RequeueFailedAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }
}
