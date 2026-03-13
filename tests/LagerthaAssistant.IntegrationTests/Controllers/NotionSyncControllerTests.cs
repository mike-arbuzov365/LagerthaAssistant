namespace LagerthaAssistant.IntegrationTests.Controllers;

using LagerthaAssistant.Api.Contracts;
using LagerthaAssistant.Api.Controllers;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Vocabulary;
using Microsoft.AspNetCore.Mvc;
using Xunit;

public sealed class NotionSyncControllerTests
{
    [Fact]
    public async Task GetStatus_ShouldReturnSyncStatus()
    {
        var processor = new FakeNotionSyncProcessor
        {
            Status = new NotionSyncStatusSummary(
                Enabled: true,
                IsConfigured: true,
                Message: "Configured",
                PendingCards: 7,
                FailedCards: 2)
        };

        var sut = new NotionSyncController(processor);

        var response = await sut.GetStatus(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<NotionSyncStatusResponse>(ok.Value);
        Assert.Equal(7, payload.PendingCards);
        Assert.Equal(2, payload.FailedCards);
    }

    [Fact]
    public async Task Run_ShouldReturnBadRequest_WhenTakeIsInvalid()
    {
        var processor = new FakeNotionSyncProcessor();
        var sut = new NotionSyncController(processor);

        var response = await sut.Run(0, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(response.Result);
        Assert.Equal(0, processor.ProcessCalls);
    }

    [Fact]
    public async Task Run_ShouldReturnSummary_WhenTakeIsValid()
    {
        var processor = new FakeNotionSyncProcessor
        {
            Summary = new NotionSyncRunSummary(
                Requested: 5,
                Processed: 5,
                Completed: 4,
                Requeued: 1,
                Failed: 0,
                PendingAfterRun: 3)
        };

        var sut = new NotionSyncController(processor);
        var response = await sut.Run(25, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<NotionSyncRunResponse>(ok.Value);
        Assert.Equal(5, payload.Requested);
        Assert.Equal(4, payload.Completed);
        Assert.Equal(3, payload.PendingAfterRun);
        Assert.Equal(25, processor.LastTake);
    }

    [Fact]
    public async Task GetFailed_ShouldReturnBadRequest_WhenTakeIsInvalid()
    {
        var processor = new FakeNotionSyncProcessor();
        var sut = new NotionSyncController(processor);

        var response = await sut.GetFailed(0, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(response.Result);
    }

    [Fact]
    public async Task GetFailed_ShouldReturnFailedCards()
    {
        var processor = new FakeNotionSyncProcessor
        {
            FailedCards =
            [
                new NotionSyncFailedCard(
                    CardId: 22,
                    Word: "void",
                    DeckFileName: "wm-nouns-ua-en.xlsx",
                    StorageMode: "local",
                    AttemptCount: 8,
                    LastError: "Retry limit reached",
                    LastAttemptAtUtc: DateTimeOffset.UtcNow,
                    LastSeenAtUtc: DateTimeOffset.UtcNow.AddMinutes(-3))
            ]
        };

        var sut = new NotionSyncController(processor);
        var response = await sut.GetFailed(20, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsAssignableFrom<IReadOnlyList<NotionSyncFailedCardResponse>>(ok.Value);
        var item = Assert.Single(payload);
        Assert.Equal(22, item.CardId);
        Assert.Equal("void", item.Word);
    }

    [Fact]
    public async Task RetryFailed_ShouldReturnRequeueSummary()
    {
        var processor = new FakeNotionSyncProcessor
        {
            RequeueResult = 4,
            Status = new NotionSyncStatusSummary(
                Enabled: true,
                IsConfigured: true,
                Message: "Configured",
                PendingCards: 9,
                FailedCards: 1)
        };

        var sut = new NotionSyncController(processor);
        var response = await sut.RetryFailed(15, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<NotionSyncRetryFailedResponse>(ok.Value);
        Assert.Equal(15, payload.Requested);
        Assert.Equal(4, payload.Requeued);
        Assert.Equal(9, payload.PendingAfterRequeue);
    }

    private sealed class FakeNotionSyncProcessor : INotionSyncProcessor
    {
        public NotionSyncStatusSummary Status { get; set; } = new(
            Enabled: false,
            IsConfigured: false,
            Message: "Disabled",
            PendingCards: 0,
            FailedCards: 0);

        public NotionSyncRunSummary Summary { get; set; } = new(
            Requested: 0,
            Processed: 0,
            Completed: 0,
            Requeued: 0,
            Failed: 0,
            PendingAfterRun: 0);

        public IReadOnlyList<NotionSyncFailedCard> FailedCards { get; set; } = [];

        public int RequeueResult { get; set; }

        public int ProcessCalls { get; private set; }

        public int LastTake { get; private set; }

        public Task<NotionSyncStatusSummary> GetStatusAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Status);

        public Task<NotionSyncRunSummary> ProcessPendingAsync(int take, CancellationToken cancellationToken = default)
        {
            ProcessCalls++;
            LastTake = take;
            return Task.FromResult(Summary);
        }

        public Task<IReadOnlyList<NotionSyncFailedCard>> GetFailedCardsAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult(FailedCards);

        public Task<int> RequeueFailedAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult(RequeueResult);
    }
}

