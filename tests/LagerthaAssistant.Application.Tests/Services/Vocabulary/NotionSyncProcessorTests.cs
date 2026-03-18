namespace LagerthaAssistant.Application.Tests.Services.Vocabulary;

using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Interfaces.Repositories;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.Application.Services.Vocabulary;
using LagerthaAssistant.Domain.Entities;
using LagerthaAssistant.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public sealed class NotionSyncProcessorTests
{
    [Fact]
    public async Task ProcessPendingAsync_ShouldCompleteCard_WhenExportSucceeded()
    {
        var repository = new FakeVocabularyCardRepository();
        repository.Cards.Add(new VocabularyCard
        {
            Id = 42,
            Word = "void",
            NormalizedWord = "void",
            Meaning = "(n) emptiness",
            Examples = "The function returns void.",
            DeckFileName = "wm-nouns-ua-en.xlsx",
            DeckPath = "C:/deck/wm-nouns-ua-en.xlsx",
            LastKnownRowNumber = 11,
            StorageMode = "local",
            LastSeenAtUtc = DateTimeOffset.UtcNow,
            NotionSyncStatus = NotionSyncStatus.Pending
        });

        var exportService = new FakeNotionCardExportService
        {
            ExportResult = new NotionCardExportResult(NotionCardExportOutcome.Created, false, PageId: "page-1")
        };
        var unitOfWork = new FakeUnitOfWork();

        var sut = new NotionSyncProcessor(
            repository,
            exportService,
            unitOfWork,
            NullLogger<NotionSyncProcessor>.Instance);

        var summary = await sut.ProcessPendingAsync(10);

        Assert.Equal(1, summary.Requested);
        Assert.Equal(1, summary.Processed);
        Assert.Equal(1, summary.Completed);
        Assert.Equal(0, summary.Requeued);
        Assert.Equal(0, summary.Failed);
        Assert.Equal(0, summary.PendingAfterRun);

        var card = Assert.Single(repository.Cards);
        Assert.Equal(NotionSyncStatus.Synced, card.NotionSyncStatus);
        Assert.Equal("page-1", card.NotionPageId);
        Assert.Equal(1, card.NotionAttemptCount);
        Assert.NotNull(card.NotionSyncedAtUtc);
        Assert.Null(card.NotionLastError);
    }

    [Fact]
    public async Task ProcessPendingAsync_ShouldRequeueCard_WhenRecoverableFailureReturned()
    {
        var repository = new FakeVocabularyCardRepository();
        repository.Cards.Add(new VocabularyCard
        {
            Id = 43,
            Word = "prepare",
            NormalizedWord = "prepare",
            Meaning = "(v) get ready",
            Examples = "We prepare release notes.",
            DeckFileName = "wm-verbs-us-en.xlsx",
            DeckPath = "C:/deck/wm-verbs-us-en.xlsx",
            LastKnownRowNumber = 32,
            StorageMode = "local",
            LastSeenAtUtc = DateTimeOffset.UtcNow,
            NotionSyncStatus = NotionSyncStatus.Pending
        });

        var exportService = new FakeNotionCardExportService
        {
            ExportResult = new NotionCardExportResult(
                NotionCardExportOutcome.Failed,
                IsRecoverableFailure: true,
                ErrorMessage: "HTTP 503 service unavailable")
        };
        var unitOfWork = new FakeUnitOfWork();

        var sut = new NotionSyncProcessor(
            repository,
            exportService,
            unitOfWork,
            NullLogger<NotionSyncProcessor>.Instance);

        var summary = await sut.ProcessPendingAsync(10);

        Assert.Equal(1, summary.Processed);
        Assert.Equal(0, summary.Completed);
        Assert.Equal(1, summary.Requeued);
        Assert.Equal(0, summary.Failed);
        Assert.Equal(1, summary.PendingAfterRun);

        var card = Assert.Single(repository.Cards);
        Assert.Equal(NotionSyncStatus.Pending, card.NotionSyncStatus);
        Assert.Equal(1, card.NotionAttemptCount);
        Assert.Contains("503", card.NotionLastError ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProcessPendingAsync_ShouldFailCard_WhenRecoverableFailureReachedRetryLimit()
    {
        var repository = new FakeVocabularyCardRepository();
        repository.Cards.Add(new VocabularyCard
        {
            Id = 44,
            Word = "commit",
            NormalizedWord = "commit",
            Meaning = "(v) commit",
            Examples = "Commit the changes.",
            DeckFileName = "wm-verbs-us-en.xlsx",
            DeckPath = "C:/deck/wm-verbs-us-en.xlsx",
            LastKnownRowNumber = 55,
            StorageMode = "graph",
            LastSeenAtUtc = DateTimeOffset.UtcNow,
            NotionSyncStatus = NotionSyncStatus.Pending,
            NotionAttemptCount = 7
        });

        var exportService = new FakeNotionCardExportService
        {
            ExportResult = new NotionCardExportResult(
                NotionCardExportOutcome.Failed,
                IsRecoverableFailure: true,
                ErrorMessage: "HTTP 503 service unavailable")
        };
        var unitOfWork = new FakeUnitOfWork();

        var sut = new NotionSyncProcessor(
            repository,
            exportService,
            unitOfWork,
            NullLogger<NotionSyncProcessor>.Instance);

        var summary = await sut.ProcessPendingAsync(10);

        Assert.Equal(1, summary.Failed);
        Assert.Equal(0, summary.Requeued);
        Assert.Equal(0, summary.PendingAfterRun);

        var card = Assert.Single(repository.Cards);
        Assert.Equal(NotionSyncStatus.Failed, card.NotionSyncStatus);
        Assert.Equal(8, card.NotionAttemptCount);
        Assert.Contains("Retry limit reached", card.NotionLastError ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetStatusAsync_ShouldReturnExportAndQueueStatus()
    {
        var repository = new FakeVocabularyCardRepository();
        repository.Cards.Add(new VocabularyCard
        {
            Id = 100,
            Word = "void",
            NormalizedWord = "void",
            Meaning = "(n) emptiness",
            Examples = "The function returns void.",
            DeckFileName = "wm-nouns-ua-en.xlsx",
            DeckPath = "C:/deck/wm-nouns-ua-en.xlsx",
            LastKnownRowNumber = 11,
            StorageMode = "local",
            LastSeenAtUtc = DateTimeOffset.UtcNow,
            NotionSyncStatus = NotionSyncStatus.Pending
        });
        repository.Cards.Add(new VocabularyCard
        {
            Id = 101,
            Word = "prepare",
            NormalizedWord = "prepare",
            Meaning = "(v) get ready",
            Examples = "We prepare release notes.",
            DeckFileName = "wm-verbs-us-en.xlsx",
            DeckPath = "C:/deck/wm-verbs-us-en.xlsx",
            LastKnownRowNumber = 22,
            StorageMode = "local",
            LastSeenAtUtc = DateTimeOffset.UtcNow,
            NotionSyncStatus = NotionSyncStatus.Failed
        });

        var exportService = new FakeNotionCardExportService
        {
            Status = new NotionExportStatus(true, true, "Configured")
        };

        var sut = new NotionSyncProcessor(
            repository,
            exportService,
            new FakeUnitOfWork(),
            NullLogger<NotionSyncProcessor>.Instance);

        var status = await sut.GetStatusAsync();

        Assert.True(status.Enabled);
        Assert.True(status.IsConfigured);
        Assert.Equal(1, status.PendingCards);
        Assert.Equal(1, status.FailedCards);
    }

    private sealed class FakeVocabularyCardRepository : IVocabularyCardRepository
    {
        public List<VocabularyCard> Cards { get; } = [];

        public Task<IReadOnlyList<VocabularyCard>> FindByAnyTokenAsync(
            IReadOnlyCollection<string> normalizedTokens,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<VocabularyCard>>([]);

        public Task<VocabularyCard?> GetByIdentityAsync(
            string normalizedWord,
            string deckFileName,
            string storageMode,
            CancellationToken cancellationToken = default)
            => Task.FromResult<VocabularyCard?>(null);

        public Task AddAsync(VocabularyCard card, CancellationToken cancellationToken = default)
        {
            Cards.Add(card);
            return Task.CompletedTask;
        }

        public Task<int> CountPendingNotionSyncAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Cards.Count(card => card.NotionSyncStatus == NotionSyncStatus.Pending));

        public Task<int> CountFailedNotionSyncAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Cards.Count(card => card.NotionSyncStatus == NotionSyncStatus.Failed));

        public Task<IReadOnlyList<VocabularyCard>> ClaimPendingNotionSyncAsync(
            int take,
            DateTimeOffset claimedAtUtc,
            CancellationToken cancellationToken = default)
        {
            var claimed = Cards
                .Where(card => card.NotionSyncStatus == NotionSyncStatus.Pending)
                .OrderBy(card => card.Id)
                .Take(Math.Max(0, take))
                .ToList();

            foreach (var card in claimed)
            {
                card.NotionSyncStatus = NotionSyncStatus.Processing;
                card.NotionAttemptCount += 1;
                card.NotionLastAttemptAtUtc = claimedAtUtc;
            }

            return Task.FromResult<IReadOnlyList<VocabularyCard>>(claimed);
        }

        public Task<IReadOnlyList<VocabularyCard>> GetFailedNotionSyncAsync(
            int take,
            CancellationToken cancellationToken = default)
        {
            var failed = Cards
                .Where(card => card.NotionSyncStatus == NotionSyncStatus.Failed)
                .OrderByDescending(card => card.NotionLastAttemptAtUtc ?? card.LastSeenAtUtc)
                .Take(Math.Max(0, take))
                .ToList();

            return Task.FromResult<IReadOnlyList<VocabularyCard>>(failed);
        }

        public Task<int> RequeueFailedNotionSyncAsync(
            int take,
            DateTimeOffset requeuedAtUtc,
            CancellationToken cancellationToken = default)
        {
            var failed = Cards
                .Where(card => card.NotionSyncStatus == NotionSyncStatus.Failed)
                .OrderByDescending(card => card.NotionLastAttemptAtUtc ?? card.LastSeenAtUtc)
                .Take(Math.Max(0, take))
                .ToList();

            foreach (var card in failed)
            {
                card.NotionSyncStatus = NotionSyncStatus.Pending;
                card.NotionAttemptCount = 0;
                card.NotionLastError = null;
                card.NotionLastAttemptAtUtc = null;
            }

            return Task.FromResult(failed.Count);
        }

        public Task<int> CountAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Cards.Count);

        public Task<IReadOnlyList<VocabularyCard>> GetRecentAsync(
            int take,
            CancellationToken cancellationToken = default)
        {
            var cards = Cards
                .OrderByDescending(card => card.FirstSeenAtUtc)
                .ThenByDescending(card => card.Id)
                .Take(Math.Max(0, take))
                .ToList();

            return Task.FromResult<IReadOnlyList<VocabularyCard>>(cards);
        }

        public Task<int> DeleteAllAsync(CancellationToken cancellationToken = default)
        {
            var count = Cards.Count;
            Cards.Clear();
            return Task.FromResult(count);
        }
    }

    private sealed class FakeNotionCardExportService : INotionCardExportService
    {
        public NotionExportStatus Status { get; set; } = new(true, true, "Configured");

        public NotionCardExportResult ExportResult { get; set; } =
            new(NotionCardExportOutcome.Created, IsRecoverableFailure: false);

        public NotionExportStatus GetStatus() => Status;

        public Task<NotionCardExportResult> ExportAsync(
            NotionCardExportRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ExportResult);
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(1);

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
}

