namespace LagerthaAssistant.Application.Tests.Services.Agents;

using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Interfaces.Repositories;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Services.Agents;
using LagerthaAssistant.Domain.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public sealed class ConversationMetricsServiceTests
{
    [Fact]
    public async Task TrackAsync_ShouldIncrementMetric_AndSaveChanges()
    {
        var repository = new FakeConversationIntentMetricRepository();
        var unitOfWork = new FakeUnitOfWork();
        var clock = new FakeClock { UtcNowValue = new DateTimeOffset(2026, 03, 12, 10, 00, 00, TimeSpan.Zero) };

        var sut = new ConversationMetricsService(
            repository,
            unitOfWork,
            clock,
            NullLogger<ConversationMetricsService>.Instance);

        var result = new ConversationAgentResult(
            "command-agent",
            "command.history",
            false,
            []);

        await sut.TrackAsync("api", result);

        var tracked = Assert.Single(repository.Rows);
        Assert.Equal(new DateTime(2026, 03, 12), tracked.MetricDateUtc);
        Assert.Equal("api", tracked.Channel);
        Assert.Equal("command-agent", tracked.AgentName);
        Assert.Equal("command.history", tracked.Intent);
        Assert.False(tracked.IsBatch);
        Assert.Equal(0, tracked.ItemsCount);
        Assert.Equal(1, unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task TrackAsync_ShouldNormalizeAndTrimValuesToStorageLimits()
    {
        var repository = new FakeConversationIntentMetricRepository();
        var unitOfWork = new FakeUnitOfWork();
        var clock = new FakeClock { UtcNowValue = new DateTimeOffset(2026, 03, 12, 10, 00, 00, TimeSpan.Zero) };

        var sut = new ConversationMetricsService(
            repository,
            unitOfWork,
            clock,
            NullLogger<ConversationMetricsService>.Instance);

        var result = new ConversationAgentResult(
            new string('B', 150),
            new string('C', 140),
            false,
            []);

        await sut.TrackAsync(new string('A', 80), result);

        var tracked = Assert.Single(repository.Rows);
        Assert.Equal(new string('a', 64), tracked.Channel);
        Assert.Equal(new string('b', 128), tracked.AgentName);
        Assert.Equal(new string('c', 128), tracked.Intent);
    }

    [Fact]
    public async Task GetTopIntentsAsync_ShouldClampDaysAndTake()
    {
        var repository = new FakeConversationIntentMetricRepository();
        var unitOfWork = new FakeUnitOfWork();
        var clock = new FakeClock { UtcNowValue = new DateTimeOffset(2026, 03, 12, 10, 00, 00, TimeSpan.Zero) };

        repository.TopResults =
        [
            new ConversationIntentMetricSummary("api", "command-agent", "command.help", false, 3, 3, clock.UtcNow)
        ];

        var sut = new ConversationMetricsService(
            repository,
            unitOfWork,
            clock,
            NullLogger<ConversationMetricsService>.Instance);

        var result = await sut.GetTopIntentsAsync(days: 999, take: 999, channel: "API");

        Assert.Single(result);
        Assert.Equal(new DateTime(2025, 12, 13), repository.LastFromDateUtc);
        Assert.Equal("api", repository.LastChannel);
        Assert.Equal(200, repository.LastTake);
    }

    private sealed class FakeConversationIntentMetricRepository : IConversationIntentMetricRepository
    {
        public List<Row> Rows { get; } = [];

        public DateTime LastFromDateUtc { get; private set; }

        public string? LastChannel { get; private set; }

        public int LastTake { get; private set; }

        public IReadOnlyList<ConversationIntentMetricSummary> TopResults { get; set; } = [];

        public Task IncrementAsync(
            DateTime metricDateUtc,
            string channel,
            string agentName,
            string intent,
            bool isBatch,
            int itemsCount,
            DateTimeOffset lastSeenAtUtc,
            CancellationToken cancellationToken = default)
        {
            Rows.Add(new Row(metricDateUtc, channel, agentName, intent, isBatch, itemsCount, lastSeenAtUtc));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ConversationIntentMetricSummary>> GetTopAsync(
            DateTime fromDateUtc,
            string? channel,
            int take,
            CancellationToken cancellationToken = default)
        {
            LastFromDateUtc = fromDateUtc;
            LastChannel = channel;
            LastTake = take;
            return Task.FromResult(TopResults);
        }

        public sealed record Row(
            DateTime MetricDateUtc,
            string Channel,
            string AgentName,
            string Intent,
            bool IsBatch,
            int ItemsCount,
            DateTimeOffset LastSeenAtUtc);
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCalls { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCalls++;
            return Task.FromResult(1);
        }

        public Task BeginTransactionAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task CommitTransactionAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public void Dispose()
        {
        }
    }

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNowValue { get; set; } = DateTimeOffset.UtcNow;

        public DateTimeOffset UtcNow => UtcNowValue;
    }
}
