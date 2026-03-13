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

        public Task<int> CountPendingAsync(CancellationToken cancellationToken = default)
        {
            var count = Jobs.Count(job => job.Status == VocabularySyncJobStatus.Pending);
            return Task.FromResult(count);
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
