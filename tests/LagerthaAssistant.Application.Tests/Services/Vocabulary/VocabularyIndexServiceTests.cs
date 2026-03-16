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

public sealed class VocabularyIndexServiceTests
{
    [Fact]
    public async Task FindByInputAsync_ShouldReturnIndexedMatches_ByToken()
    {
        var cardRepo = new FakeVocabularyCardRepository();
        var syncRepo = new FakeVocabularySyncJobRepository();
        var parser = new VocabularyReplyParser();
        var unitOfWork = new FakeUnitOfWork();

        var card = new VocabularyCard
        {
            Id = 1,
            Word = "void",
            NormalizedWord = "void",
            Meaning = "(n) emptiness",
            Examples = "The function returns void.",
            DeckFileName = "wm-nouns-ua-en.xlsx",
            DeckPath = "C:/deck/wm-nouns-ua-en.xlsx",
            LastKnownRowNumber = 11,
            StorageMode = "local",
            SyncStatus = VocabularySyncStatus.Synced,
            FirstSeenAtUtc = DateTimeOffset.UtcNow,
            LastSeenAtUtc = DateTimeOffset.UtcNow
        };

        card.Tokens.Add(new VocabularyCardToken { TokenNormalized = "void" });
        cardRepo.Cards.Add(card);

        var sut = new VocabularyIndexService(cardRepo, syncRepo, parser, unitOfWork, NullLogger<VocabularyIndexService>.Instance);

        var lookup = await sut.FindByInputAsync("void");

        Assert.True(lookup.Found);
        var match = Assert.Single(lookup.Matches);
        Assert.Equal("void", match.Word);
        Assert.Equal("wm-nouns-ua-en.xlsx", match.DeckFileName);
    }

    [Fact]
    public async Task FindByInputsAsync_ShouldReturnLookupPerInput_UsingSingleTokenQuery()
    {
        var cardRepo = new FakeVocabularyCardRepository();
        var syncRepo = new FakeVocabularySyncJobRepository();
        var parser = new VocabularyReplyParser();
        var unitOfWork = new FakeUnitOfWork();

        var voidCard = new VocabularyCard
        {
            Id = 1,
            Word = "void",
            NormalizedWord = "void",
            Meaning = "(n) emptiness",
            Examples = "The function returns void.",
            DeckFileName = "wm-nouns-ua-en.xlsx",
            DeckPath = "C:/deck/wm-nouns-ua-en.xlsx",
            LastKnownRowNumber = 11,
            StorageMode = "local",
            SyncStatus = VocabularySyncStatus.Synced,
            FirstSeenAtUtc = DateTimeOffset.UtcNow,
            LastSeenAtUtc = DateTimeOffset.UtcNow
        };
        voidCard.Tokens.Add(new VocabularyCardToken { TokenNormalized = "void" });
        cardRepo.Cards.Add(voidCard);

        var prepareCard = new VocabularyCard
        {
            Id = 2,
            Word = "prepare",
            NormalizedWord = "prepare",
            Meaning = "(v) get ready",
            Examples = "We prepare release notes.",
            DeckFileName = "wm-verbs-us-en.xlsx",
            DeckPath = "C:/deck/wm-verbs-us-en.xlsx",
            LastKnownRowNumber = 27,
            StorageMode = "local",
            SyncStatus = VocabularySyncStatus.Synced,
            FirstSeenAtUtc = DateTimeOffset.UtcNow,
            LastSeenAtUtc = DateTimeOffset.UtcNow
        };
        prepareCard.Tokens.Add(new VocabularyCardToken { TokenNormalized = "prepare" });
        cardRepo.Cards.Add(prepareCard);

        var sut = new VocabularyIndexService(cardRepo, syncRepo, parser, unitOfWork, NullLogger<VocabularyIndexService>.Instance);

        var lookups = await sut.FindByInputsAsync(["void", "prepare", "unknown"]);

        Assert.Equal(1, cardRepo.FindByAnyTokenCalls);
        Assert.Equal(3, lookups.Count);
        Assert.True(lookups["void"].Found);
        Assert.True(lookups["prepare"].Found);
        Assert.False(lookups["unknown"].Found);
    }

    [Fact]
    public async Task FindByInputAsync_ShouldNormalizeUnicodeDashVariants_WhenTokenizing()
    {
        var cardRepo = new FakeVocabularyCardRepository();
        var syncRepo = new FakeVocabularySyncJobRepository();
        var parser = new VocabularyReplyParser();
        var unitOfWork = new FakeUnitOfWork();

        var card = new VocabularyCard
        {
            Id = 1,
            Word = "undertake - undertook - undertaken",
            NormalizedWord = "undertake - undertook - undertaken",
            Meaning = "(iv) братися за щось",
            Examples = "The team undertook a redesign.",
            DeckFileName = "wm-irregular-verbs-ua-en.xlsx",
            DeckPath = "C:/deck/wm-irregular-verbs-ua-en.xlsx",
            LastKnownRowNumber = 81,
            StorageMode = "local",
            SyncStatus = VocabularySyncStatus.Synced,
            FirstSeenAtUtc = DateTimeOffset.UtcNow,
            LastSeenAtUtc = DateTimeOffset.UtcNow
        };

        card.Tokens.Add(new VocabularyCardToken { TokenNormalized = "undertook" });
        cardRepo.Cards.Add(card);

        var sut = new VocabularyIndexService(cardRepo, syncRepo, parser, unitOfWork, NullLogger<VocabularyIndexService>.Instance);

        var query = "undertake \u2013 undertook \u2014 undertaken";
        var lookup = await sut.FindByInputAsync(query);

        Assert.True(lookup.Found);
        var match = Assert.Single(lookup.Matches);
        Assert.Equal("undertake - undertook - undertaken", match.Word);
    }

    [Fact]
    public async Task FindByInputsAsync_ShouldNotDuplicateMatches_WhenSeveralTokensPointToSameCard()
    {
        var cardRepo = new FakeVocabularyCardRepository();
        var syncRepo = new FakeVocabularySyncJobRepository();
        var parser = new VocabularyReplyParser();
        var unitOfWork = new FakeUnitOfWork();

        var card = new VocabularyCard
        {
            Id = 1,
            Word = "undertake - undertook - undertaken",
            NormalizedWord = "undertake - undertook - undertaken",
            Meaning = "(iv) братися за щось",
            Examples = "The team undertook a redesign.",
            DeckFileName = "wm-irregular-verbs-ua-en.xlsx",
            DeckPath = "C:/deck/wm-irregular-verbs-ua-en.xlsx",
            LastKnownRowNumber = 81,
            StorageMode = "local",
            SyncStatus = VocabularySyncStatus.Synced,
            FirstSeenAtUtc = DateTimeOffset.UtcNow,
            LastSeenAtUtc = DateTimeOffset.UtcNow
        };

        card.Tokens.Add(new VocabularyCardToken { TokenNormalized = "undertake" });
        card.Tokens.Add(new VocabularyCardToken { TokenNormalized = "undertook" });
        card.Tokens.Add(new VocabularyCardToken { TokenNormalized = "undertaken" });
        cardRepo.Cards.Add(card);

        var sut = new VocabularyIndexService(cardRepo, syncRepo, parser, unitOfWork, NullLogger<VocabularyIndexService>.Instance);

        var lookups = await sut.FindByInputsAsync(["undertake - undertook - undertaken"]);

        var lookup = lookups["undertake - undertook - undertaken"];
        Assert.True(lookup.Found);
        Assert.Single(lookup.Matches);
    }

    [Fact]
    public async Task HandleAppendResultAsync_ShouldUpsertSyncedCard_WhenAppendSucceeded()
    {
        var cardRepo = new FakeVocabularyCardRepository();
        var syncRepo = new FakeVocabularySyncJobRepository();
        var parser = new VocabularyReplyParser();
        var unitOfWork = new FakeUnitOfWork();

        var sut = new VocabularyIndexService(cardRepo, syncRepo, parser, unitOfWork, NullLogger<VocabularyIndexService>.Instance);

        var appendResult = new VocabularyAppendResult(
            VocabularyAppendStatus.Added,
            new VocabularyDeckEntry(
                "wm-irregular-verbs-ua-en.xlsx",
                "C:/deck/wm-irregular-verbs-ua-en.xlsx",
                81,
                "undertake - undertook - undertaken",
                "(iv) take on something",
                "We undertake infrastructure improvements."));

        var reply = """
undertake - undertook - undertaken

(iv) take on something

We undertake infrastructure improvements every quarter

Last month the team undertook a major API redesign

The migration has been undertaken by the platform team
""";

        await sut.HandleAppendResultAsync(
            "undertake",
            reply,
            "wm-irregular-verbs-ua-en.xlsx",
            null,
            appendResult,
            VocabularyStorageMode.Local);

        var card = Assert.Single(cardRepo.Cards);
        Assert.Equal(VocabularySyncStatus.Synced, card.SyncStatus);
        Assert.Equal("undertake - undertook - undertaken", card.Word);
        Assert.Contains(card.Tokens, token => token.TokenNormalized == "undertake");
        Assert.Contains(card.Tokens, token => token.TokenNormalized == "undertook");
        Assert.Contains(card.Tokens, token => token.TokenNormalized == "undertaken");
        Assert.Equal(1, unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task HandleAppendResultAsync_ShouldCreatePendingSyncJob_WhenFileLocked()
    {
        var cardRepo = new FakeVocabularyCardRepository();
        var syncRepo = new FakeVocabularySyncJobRepository();
        var parser = new VocabularyReplyParser();
        var unitOfWork = new FakeUnitOfWork();

        var sut = new VocabularyIndexService(cardRepo, syncRepo, parser, unitOfWork, NullLogger<VocabularyIndexService>.Instance);

        var errorResult = new VocabularyAppendResult(
            VocabularyAppendStatus.Error,
            Message: "Failed to append vocabulary card: file is open in another app.");

        var reply = """
void

(n) emptiness

The function returns void when there is no value to return
""";

        await sut.HandleAppendResultAsync(
            "void",
            reply,
            "wm-nouns-ua-en.xlsx",
            null,
            errorResult,
            VocabularyStorageMode.Local);

        var job = Assert.Single(syncRepo.Jobs);
        Assert.Equal(VocabularySyncJobStatus.Pending, job.Status);
        Assert.Equal("wm-nouns-ua-en.xlsx", job.TargetDeckFileName);

        var card = Assert.Single(cardRepo.Cards);
        Assert.Equal(VocabularySyncStatus.Pending, card.SyncStatus);
        Assert.Equal(1, unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task HandleAppendResultAsync_ShouldReuseActivePendingJob_WhenDuplicateQueuedJobExists()
    {
        var cardRepo = new FakeVocabularyCardRepository();
        var syncRepo = new FakeVocabularySyncJobRepository();
        var parser = new VocabularyReplyParser();
        var unitOfWork = new FakeUnitOfWork();

        syncRepo.Jobs.Add(new VocabularySyncJob
        {
            RequestedWord = "void",
            AssistantReply = "void\n\n(n) emptiness",
            TargetDeckFileName = "wm-nouns-ua-en.xlsx",
            StorageMode = "local",
            OverridePartOfSpeech = null,
            Status = VocabularySyncJobStatus.Pending,
            CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-3)
        });

        var sut = new VocabularyIndexService(cardRepo, syncRepo, parser, unitOfWork, NullLogger<VocabularyIndexService>.Instance);

        var errorResult = new VocabularyAppendResult(
            VocabularyAppendStatus.Error,
            Message: "Failed to append vocabulary card: file is open in another app.");

        await sut.HandleAppendResultAsync(
            "void",
            "void\n\n(n) emptiness",
            "wm-nouns-ua-en.xlsx",
            null,
            errorResult,
            VocabularyStorageMode.Local);

        Assert.Single(syncRepo.Jobs);
        var job = syncRepo.Jobs[0];
        Assert.Equal(VocabularySyncJobStatus.Pending, job.Status);
        Assert.Contains("open in another app", job.LastError ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ClearAsync_ShouldDeleteAllCards_AndReturnCount()
    {
        var cardRepo = new FakeVocabularyCardRepository();
        var syncRepo = new FakeVocabularySyncJobRepository();
        var parser = new VocabularyReplyParser();
        var unitOfWork = new FakeUnitOfWork();

        cardRepo.Cards.Add(new VocabularyCard { Id = 1, Word = "alpha", NormalizedWord = "alpha", DeckFileName = "deck.xlsx", DeckPath = "C:/deck.xlsx", StorageMode = "local", SyncStatus = VocabularySyncStatus.Synced, FirstSeenAtUtc = DateTimeOffset.UtcNow, LastSeenAtUtc = DateTimeOffset.UtcNow });
        cardRepo.Cards.Add(new VocabularyCard { Id = 2, Word = "beta",  NormalizedWord = "beta",  DeckFileName = "deck.xlsx", DeckPath = "C:/deck.xlsx", StorageMode = "local", SyncStatus = VocabularySyncStatus.Synced, FirstSeenAtUtc = DateTimeOffset.UtcNow, LastSeenAtUtc = DateTimeOffset.UtcNow });

        var sut = new VocabularyIndexService(cardRepo, syncRepo, parser, unitOfWork, NullLogger<VocabularyIndexService>.Instance);

        var deleted = await sut.ClearAsync();

        Assert.Equal(2, deleted);
        Assert.Empty(cardRepo.Cards);
    }

    [Fact]
    public async Task RebuildAsync_ShouldClearAndIndexAllEntries()
    {
        var cardRepo = new FakeVocabularyCardRepository();
        var syncRepo = new FakeVocabularySyncJobRepository();
        var parser = new VocabularyReplyParser();
        var unitOfWork = new FakeUnitOfWork();

        // Pre-existing stale card that should be wiped
        cardRepo.Cards.Add(new VocabularyCard { Id = 1, Word = "stale", NormalizedWord = "stale", DeckFileName = "deck.xlsx", DeckPath = "C:/deck.xlsx", StorageMode = "local", SyncStatus = VocabularySyncStatus.Synced, FirstSeenAtUtc = DateTimeOffset.UtcNow, LastSeenAtUtc = DateTimeOffset.UtcNow });

        var sut = new VocabularyIndexService(cardRepo, syncRepo, parser, unitOfWork, NullLogger<VocabularyIndexService>.Instance);

        var entries = new List<VocabularyDeckEntry>
        {
            new("wm-nouns-ua-en.xlsx", "C:/deck.xlsx", 11, "resolve", "(v) вирішувати", "We need to resolve the issue."),
            new("wm-nouns-ua-en.xlsx", "C:/deck.xlsx", 12, "deploy",  "(v) розгортати",  "We deploy every Friday.")
        };

        var indexed = await sut.RebuildAsync(entries, VocabularyStorageMode.Local);

        Assert.Equal(2, indexed);
        Assert.DoesNotContain(cardRepo.Cards, c => c.NormalizedWord == "stale");
        Assert.Contains(cardRepo.Cards, c => c.NormalizedWord == "resolve");
        Assert.Contains(cardRepo.Cards, c => c.NormalizedWord == "deploy");
    }

    [Fact]
    public async Task RebuildAsync_ShouldMergeDuplicateWordRows_WhenSameWordAppearsInMultipleRowsOfSameDeck()
    {
        // Verifies that a word stored on separate rows (e.g. verb row + noun row) is merged into a
        // single card so the unique index (NormalizedWord, DeckFileName, StorageMode) is not violated.
        var cardRepo = new FakeVocabularyCardRepository();
        var syncRepo = new FakeVocabularySyncJobRepository();
        var parser = new VocabularyReplyParser();
        var unitOfWork = new FakeUnitOfWork();

        var sut = new VocabularyIndexService(cardRepo, syncRepo, parser, unitOfWork, NullLogger<VocabularyIndexService>.Instance);

        var entries = new List<VocabularyDeckEntry>
        {
            new("wm-vocabulary-ua-en.xlsx", "C:/deck.xlsx", 11, "watch", "(v) дивитися", "Watch the video."),
            new("wm-vocabulary-ua-en.xlsx", "C:/deck.xlsx", 12, "watch", "(n) годинник",  "My watch stopped.")
        };

        var indexed = await sut.RebuildAsync(entries, VocabularyStorageMode.Local);

        // Two source rows collapse into one card
        Assert.Equal(1, indexed);
        var card = Assert.Single(cardRepo.Cards);
        Assert.Equal("watch", card.NormalizedWord);
        Assert.Contains("(v) дивитися", card.Meaning);
        Assert.Contains("(n) годинник", card.Meaning);
    }

    [Fact]
    public async Task RebuildAsync_ShouldReturnZero_WhenNoEntries()
    {
        var cardRepo = new FakeVocabularyCardRepository();
        var syncRepo = new FakeVocabularySyncJobRepository();
        var parser = new VocabularyReplyParser();
        var unitOfWork = new FakeUnitOfWork();

        cardRepo.Cards.Add(new VocabularyCard { Id = 1, Word = "stale", NormalizedWord = "stale", DeckFileName = "deck.xlsx", DeckPath = "C:/deck.xlsx", StorageMode = "local", SyncStatus = VocabularySyncStatus.Synced, FirstSeenAtUtc = DateTimeOffset.UtcNow, LastSeenAtUtc = DateTimeOffset.UtcNow });

        var sut = new VocabularyIndexService(cardRepo, syncRepo, parser, unitOfWork, NullLogger<VocabularyIndexService>.Instance);

        var indexed = await sut.RebuildAsync([], VocabularyStorageMode.Local);

        Assert.Equal(0, indexed);
        Assert.Empty(cardRepo.Cards); // stale card was deleted
    }

    private sealed class FakeVocabularyCardRepository : IVocabularyCardRepository
    {
        public List<VocabularyCard> Cards { get; } = [];

        public int FindByAnyTokenCalls { get; private set; }

        public Task<IReadOnlyList<VocabularyCard>> FindByAnyTokenAsync(IReadOnlyCollection<string> normalizedTokens, CancellationToken cancellationToken = default)
        {
            FindByAnyTokenCalls++;

            var set = normalizedTokens.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var result = Cards
                .Where(card => card.Tokens.Any(token => set.Contains(token.TokenNormalized)))
                .ToList();

            return Task.FromResult<IReadOnlyList<VocabularyCard>>(result);
        }

        public Task<VocabularyCard?> GetByIdentityAsync(string normalizedWord, string deckFileName, string storageMode, CancellationToken cancellationToken = default)
        {
            var result = Cards.FirstOrDefault(card =>
                card.NormalizedWord.Equals(normalizedWord, StringComparison.Ordinal)
                && card.DeckFileName.Equals(deckFileName, StringComparison.Ordinal)
                && card.StorageMode.Equals(storageMode, StringComparison.Ordinal));

            return Task.FromResult(result);
        }

        public Task AddAsync(VocabularyCard card, CancellationToken cancellationToken = default)
        {
            if (card.Id == 0)
            {
                card.Id = Cards.Count + 1;
            }

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

        public Task<IReadOnlyList<VocabularyCard>> GetFailedNotionSyncAsync(int take, CancellationToken cancellationToken = default)
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

        public Task<int> DeleteAllAsync(CancellationToken cancellationToken = default)
        {
            var count = Cards.Count;
            Cards.Clear();
            return Task.FromResult(count);
        }
    }

    private sealed class FakeVocabularySyncJobRepository : IVocabularySyncJobRepository
    {
        public List<VocabularySyncJob> Jobs { get; } = [];

        public Task AddAsync(VocabularySyncJob job, CancellationToken cancellationToken = default)
        {
            Jobs.Add(job);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<VocabularySyncJob>> GetPendingAsync(int take, CancellationToken cancellationToken = default)
        {
            var pending = Jobs
                .Where(job => job.Status == VocabularySyncJobStatus.Pending)
                .Take(Math.Max(0, take))
                .ToList();

            return Task.FromResult<IReadOnlyList<VocabularySyncJob>>(pending);
        }

        public Task<IReadOnlyList<VocabularySyncJob>> ClaimPendingAsync(
            int take,
            DateTimeOffset claimedAtUtc,
            CancellationToken cancellationToken = default)
        {
            var pending = Jobs
                .Where(job => job.Status == VocabularySyncJobStatus.Pending)
                .OrderBy(job => job.CreatedAtUtc)
                .Take(Math.Max(0, take))
                .ToList();

            foreach (var job in pending)
            {
                job.Status = VocabularySyncJobStatus.Processing;
                job.AttemptCount += 1;
                job.LastAttemptAtUtc = claimedAtUtc;
            }

            return Task.FromResult<IReadOnlyList<VocabularySyncJob>>(pending);
        }

        public Task<VocabularySyncJob?> FindActiveDuplicateAsync(
            string requestedWord,
            string assistantReply,
            string targetDeckFileName,
            string storageMode,
            string? overridePartOfSpeech,
            CancellationToken cancellationToken = default)
        {
            var job = Jobs.FirstOrDefault(x =>
                (x.Status == VocabularySyncJobStatus.Pending || x.Status == VocabularySyncJobStatus.Processing)
                && x.RequestedWord == requestedWord
                && x.AssistantReply == assistantReply
                && x.TargetDeckFileName == targetDeckFileName
                && x.StorageMode == storageMode
                && x.OverridePartOfSpeech == overridePartOfSpeech);

            return Task.FromResult(job);
        }

        public Task<int> CountPendingAsync(CancellationToken cancellationToken = default)
        {
            var count = Jobs.Count(job => job.Status == VocabularySyncJobStatus.Pending);
            return Task.FromResult(count);
        }

        public Task<IReadOnlyList<VocabularySyncJob>> GetFailedAsync(int take, CancellationToken cancellationToken = default)
        {
            var failed = Jobs
                .Where(job => job.Status == VocabularySyncJobStatus.Failed)
                .OrderByDescending(job => job.LastAttemptAtUtc ?? job.CreatedAtUtc)
                .Take(Math.Max(0, take))
                .ToList();

            return Task.FromResult<IReadOnlyList<VocabularySyncJob>>(failed);
        }

        public Task<int> RequeueFailedAsync(
            int take,
            DateTimeOffset requeuedAtUtc,
            CancellationToken cancellationToken = default)
        {
            var failed = Jobs
                .Where(job => job.Status == VocabularySyncJobStatus.Failed)
                .OrderByDescending(job => job.LastAttemptAtUtc ?? job.CreatedAtUtc)
                .Take(Math.Max(0, take))
                .ToList();

            foreach (var job in failed)
            {
                job.Status = VocabularySyncJobStatus.Pending;
                job.AttemptCount = 0;
                job.LastError = null;
                job.LastAttemptAtUtc = null;
                job.CompletedAtUtc = null;
            }

            return Task.FromResult(failed.Count);
        }
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
}
