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

public sealed class VocabularySyncProcessorTests
{
    [Fact]
    public async Task ProcessPendingAsync_ShouldCompleteJob_AndUpdateIndex_WhenAppendSucceeded()
    {
        var repository = new FakeVocabularySyncJobRepository();
        var deckModeService = new FakeVocabularyDeckModeService
        {
            AppendResult = new VocabularyAppendResult(
                VocabularyAppendStatus.Added,
                new VocabularyDeckEntry(
                    "wm-verbs-us-en.xlsx",
                    "C:/deck/wm-verbs-us-en.xlsx",
                    11,
                    "prepare",
                    "(v) prepare",
                    "We prepare release notes."))
        };

        var indexService = new FakeVocabularyIndexService();
        var storageModeProvider = new FakeStorageModeProvider();
        var unitOfWork = new FakeUnitOfWork();

        repository.Jobs.Add(new VocabularySyncJob
        {
            RequestedWord = "prepare",
            AssistantReply = "prepare\n\n(v) prepare\n\nWe prepare release notes.",
            TargetDeckFileName = "wm-verbs-us-en.xlsx",
            StorageMode = "local",
            Status = VocabularySyncJobStatus.Pending,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        var sut = new VocabularySyncProcessor(
            repository,
            deckModeService,
            indexService,
            storageModeProvider,
            unitOfWork,
            NullLogger<VocabularySyncProcessor>.Instance);

        var summary = await sut.ProcessPendingAsync(10);

        Assert.Equal(1, summary.Requested);
        Assert.Equal(1, summary.Processed);
        Assert.Equal(1, summary.Completed);
        Assert.Equal(0, summary.Requeued);
        Assert.Equal(0, summary.Failed);
        Assert.Equal(0, summary.PendingAfterRun);

        var job = Assert.Single(repository.Jobs);
        Assert.Equal(VocabularySyncJobStatus.Completed, job.Status);
        Assert.Equal(1, job.AttemptCount);
        Assert.NotNull(job.LastAttemptAtUtc);
        Assert.NotNull(job.CompletedAtUtc);
        Assert.Null(job.LastError);

        Assert.Equal(1, deckModeService.Calls);
        Assert.Equal(VocabularyStorageMode.Local, deckModeService.LastMode);
        Assert.Equal(1, indexService.Calls);
        Assert.Equal(2, unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task ProcessPendingAsync_ShouldRequeueJob_WhenAppendReturnsRecoverableError()
    {
        var repository = new FakeVocabularySyncJobRepository();
        var deckModeService = new FakeVocabularyDeckModeService
        {
            AppendResult = new VocabularyAppendResult(
                VocabularyAppendStatus.Error,
                Message: "Failed to append vocabulary card: file is open in another app.")
        };

        var indexService = new FakeVocabularyIndexService();
        var storageModeProvider = new FakeStorageModeProvider();
        var unitOfWork = new FakeUnitOfWork();

        repository.Jobs.Add(new VocabularySyncJob
        {
            RequestedWord = "void",
            AssistantReply = "void",
            TargetDeckFileName = "wm-nouns-ua-en.xlsx",
            StorageMode = "graph",
            Status = VocabularySyncJobStatus.Pending,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        var sut = new VocabularySyncProcessor(
            repository,
            deckModeService,
            indexService,
            storageModeProvider,
            unitOfWork,
            NullLogger<VocabularySyncProcessor>.Instance);

        var summary = await sut.ProcessPendingAsync(10);

        Assert.Equal(1, summary.Requested);
        Assert.Equal(1, summary.Processed);
        Assert.Equal(0, summary.Completed);
        Assert.Equal(1, summary.Requeued);
        Assert.Equal(0, summary.Failed);
        Assert.Equal(1, summary.PendingAfterRun);

        var job = Assert.Single(repository.Jobs);
        Assert.Equal(VocabularySyncJobStatus.Pending, job.Status);
        Assert.Equal(1, job.AttemptCount);
        Assert.NotNull(job.LastAttemptAtUtc);
        Assert.Contains("open in another app", job.LastError ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(1, deckModeService.Calls);
        Assert.Equal(VocabularyStorageMode.Graph, deckModeService.LastMode);
        Assert.Equal(0, indexService.Calls);
        Assert.Equal(2, unitOfWork.SaveCalls);
    }

    [Fact]
    public async Task ProcessPendingAsync_ShouldFailJob_WhenStorageModeIsUnknown()
    {
        var repository = new FakeVocabularySyncJobRepository();
        var deckModeService = new FakeVocabularyDeckModeService();
        var indexService = new FakeVocabularyIndexService();
        var storageModeProvider = new FakeStorageModeProvider();
        var unitOfWork = new FakeUnitOfWork();

        repository.Jobs.Add(new VocabularySyncJob
        {
            RequestedWord = "test",
            AssistantReply = "test",
            TargetDeckFileName = "wm-vocabulary-1-grade-ua-en.xlsx",
            StorageMode = "unknown-mode",
            Status = VocabularySyncJobStatus.Pending,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        var sut = new VocabularySyncProcessor(
            repository,
            deckModeService,
            indexService,
            storageModeProvider,
            unitOfWork,
            NullLogger<VocabularySyncProcessor>.Instance);

        var summary = await sut.ProcessPendingAsync(10);

        Assert.Equal(1, summary.Requested);
        Assert.Equal(1, summary.Processed);
        Assert.Equal(0, summary.Completed);
        Assert.Equal(0, summary.Requeued);
        Assert.Equal(1, summary.Failed);
        Assert.Equal(0, summary.PendingAfterRun);

        var job = Assert.Single(repository.Jobs);
        Assert.Equal(VocabularySyncJobStatus.Failed, job.Status);
        Assert.Equal(0, job.AttemptCount);
        Assert.NotNull(job.LastAttemptAtUtc);
        Assert.Contains("Unknown storage mode", job.LastError ?? string.Empty, StringComparison.Ordinal);

        Assert.Equal(0, deckModeService.Calls);
        Assert.Equal(0, indexService.Calls);
        Assert.Equal(1, unitOfWork.SaveCalls);
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
                .OrderBy(job => job.CreatedAtUtc)
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

    private sealed class FakeVocabularyDeckModeService : IVocabularyDeckModeService
    {
        public VocabularyAppendResult AppendResult { get; set; } = new(VocabularyAppendStatus.NoMatchingDeck);

        public int Calls { get; private set; }

        public VocabularyStorageMode LastMode { get; private set; }

        public Task<VocabularyAppendResult> AppendFromAssistantReplyAsync(
            VocabularyStorageMode mode,
            string requestedWord,
            string assistantReply,
            string? forcedDeckFileName = null,
            string? overridePartOfSpeech = null,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            LastMode = mode;
            return Task.FromResult(AppendResult);
        }
    }

    private sealed class FakeVocabularyIndexService : IVocabularyIndexService
    {
        public int Calls { get; private set; }

        public Task<VocabularyLookupResult> FindByInputAsync(string input, CancellationToken cancellationToken = default)
            => Task.FromResult(new VocabularyLookupResult(input, []));

        public Task<IReadOnlyDictionary<string, VocabularyLookupResult>> FindByInputsAsync(
            IReadOnlyList<string> inputs,
            CancellationToken cancellationToken = default)
        {
            var result = inputs
                .Where(input => !string.IsNullOrWhiteSpace(input))
                .ToDictionary(
                    input => input,
                    input => new VocabularyLookupResult(input, []),
                    StringComparer.OrdinalIgnoreCase);

            return Task.FromResult<IReadOnlyDictionary<string, VocabularyLookupResult>>(result);
        }

        public Task IndexLookupResultAsync(VocabularyLookupResult lookup, VocabularyStorageMode storageMode, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task HandleAppendResultAsync(
            string requestedWord,
            string assistantReply,
            string? targetDeckFileName,
            string? overridePartOfSpeech,
            VocabularyAppendResult appendResult,
            VocabularyStorageMode storageMode,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeStorageModeProvider : IVocabularyStorageModeProvider
    {
        public VocabularyStorageMode CurrentMode => VocabularyStorageMode.Local;

        public void SetMode(VocabularyStorageMode mode)
        {
        }

        public bool TryParse(string? value, out VocabularyStorageMode mode)
        {
            if (string.Equals(value, "local", StringComparison.OrdinalIgnoreCase))
            {
                mode = VocabularyStorageMode.Local;
                return true;
            }

            if (string.Equals(value, "graph", StringComparison.OrdinalIgnoreCase))
            {
                mode = VocabularyStorageMode.Graph;
                return true;
            }

            mode = VocabularyStorageMode.Local;
            return false;
        }

        public string ToText(VocabularyStorageMode mode)
            => mode.ToString().ToLowerInvariant();
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

