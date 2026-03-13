namespace LagerthaAssistant.IntegrationTests.Controllers;

using LagerthaAssistant.Api.Controllers;
using LagerthaAssistant.Api.Contracts;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Vocabulary;
using Microsoft.AspNetCore.Mvc;
using Xunit;

public sealed class VocabularySyncControllerTests
{
    [Fact]
    public async Task GetStatus_ShouldReturnPendingJobs()
    {
        var processor = new FakeVocabularySyncProcessor
        {
            PendingCount = 5
        };

        var sut = new VocabularySyncController(processor);

        var response = await sut.GetStatus(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<VocabularySyncStatusResponse>(ok.Value);
        Assert.Equal(5, payload.PendingJobs);
    }

    [Fact]
    public async Task Run_ShouldReturnBadRequest_WhenTakeIsInvalid()
    {
        var processor = new FakeVocabularySyncProcessor();
        var sut = new VocabularySyncController(processor);

        var response = await sut.Run(0, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(response.Result);
        Assert.Equal(0, processor.ProcessCalls);
    }

    [Fact]
    public async Task Run_ShouldReturnSummary_WhenTakeIsValid()
    {
        var processor = new FakeVocabularySyncProcessor
        {
            Summary = new VocabularySyncRunSummary(
                Requested: 4,
                Processed: 4,
                Completed: 3,
                Requeued: 1,
                Failed: 0,
                PendingAfterRun: 2)
        };

        var sut = new VocabularySyncController(processor);

        var response = await sut.Run(25, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<VocabularySyncRunResponse>(ok.Value);

        Assert.Equal(4, payload.Requested);
        Assert.Equal(4, payload.Processed);
        Assert.Equal(3, payload.Completed);
        Assert.Equal(1, payload.Requeued);
        Assert.Equal(0, payload.Failed);
        Assert.Equal(2, payload.PendingAfterRun);
        Assert.Equal(1, processor.ProcessCalls);
        Assert.Equal(25, processor.LastTake);
    }

    [Fact]
    public async Task GetFailed_ShouldReturnBadRequest_WhenTakeIsInvalid()
    {
        var processor = new FakeVocabularySyncProcessor();
        var sut = new VocabularySyncController(processor);

        var response = await sut.GetFailed(0, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(response.Result);
    }

    [Fact]
    public async Task GetFailed_ShouldReturnFailedJobs()
    {
        var processor = new FakeVocabularySyncProcessor
        {
            FailedJobs =
            [
                new VocabularySyncFailedJob(
                    12,
                    "void",
                    "wm-nouns-ua-en.xlsx",
                    "local",
                    8,
                    "Retry limit reached",
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow.AddMinutes(-3))
            ]
        };

        var sut = new VocabularySyncController(processor);
        var response = await sut.GetFailed(20, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsAssignableFrom<IReadOnlyList<VocabularySyncFailedJobResponse>>(ok.Value);
        var item = Assert.Single(payload);
        Assert.Equal(12, item.Id);
        Assert.Equal("void", item.RequestedWord);
        Assert.Equal(20, processor.LastFailedTake);
    }

    [Fact]
    public async Task RetryFailed_ShouldReturnRequeueSummary()
    {
        var processor = new FakeVocabularySyncProcessor
        {
            RequeueResult = 4,
            PendingCount = 9
        };

        var sut = new VocabularySyncController(processor);
        var response = await sut.RetryFailed(15, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<VocabularySyncRetryFailedResponse>(ok.Value);
        Assert.Equal(15, payload.Requested);
        Assert.Equal(4, payload.Requeued);
        Assert.Equal(9, payload.PendingAfterRequeue);
        Assert.Equal(15, processor.LastRequeueTake);
    }

    private sealed class FakeVocabularySyncProcessor : IVocabularySyncProcessor
    {
        public int PendingCount { get; set; }

        public IReadOnlyList<VocabularySyncFailedJob> FailedJobs { get; set; } = [];

        public int RequeueResult { get; set; }

        public int ProcessCalls { get; private set; }

        public int LastTake { get; private set; }

        public int LastFailedTake { get; private set; }

        public int LastRequeueTake { get; private set; }

        public VocabularySyncRunSummary Summary { get; set; } = new(
            Requested: 0,
            Processed: 0,
            Completed: 0,
            Requeued: 0,
            Failed: 0,
            PendingAfterRun: 0);

        public Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(PendingCount);

        public Task<VocabularySyncRunSummary> ProcessPendingAsync(int take, CancellationToken cancellationToken = default)
        {
            ProcessCalls++;
            LastTake = take;
            return Task.FromResult(Summary);
        }

        public Task<IReadOnlyList<VocabularySyncFailedJob>> GetFailedJobsAsync(int take, CancellationToken cancellationToken = default)
        {
            LastFailedTake = take;
            return Task.FromResult(FailedJobs);
        }

        public Task<int> RequeueFailedAsync(int take, CancellationToken cancellationToken = default)
        {
            LastRequeueTake = take;
            return Task.FromResult(RequeueResult);
        }
    }
}
