namespace LagerthaAssistant.Application.Tests.Services.Vocabulary;

using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.Application.Services.Vocabulary;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public sealed class VocabularyPersistenceServiceTests
{
    [Fact]
    public async Task AppendFromAssistantReplyAsync_ShouldForwardResult_AndTrackInSqlIndex()
    {
        var expectedResult = new VocabularyAppendResult(
            VocabularyAppendStatus.Added,
            new VocabularyDeckEntry(
                "wm-verbs-us-en.xlsx",
                "C:/deck/wm-verbs-us-en.xlsx",
                21,
                "prepare",
                "(v) prepare",
                "We prepare release notes."));

        var deckService = new FakeVocabularyDeckModeService(expectedResult);
        var indexService = new FakeVocabularyIndexService();
        var modeProvider = new FakeStorageModeProvider();

        var sut = new VocabularyPersistenceService(
            deckService,
            indexService,
            modeProvider,
            NullLogger<VocabularyPersistenceService>.Instance);

        var result = await sut.AppendFromAssistantReplyAsync(
            "prepare",
            "prepare\n\n(v) prepare\n\nWe prepare release notes.",
            "wm-verbs-us-en.xlsx",
            "v");

        Assert.Same(expectedResult, result);
        Assert.Equal(1, deckService.Calls);
        Assert.Equal(1, indexService.Calls);
        Assert.Equal(VocabularyStorageMode.Graph, indexService.LastMode);
        Assert.Equal("prepare", indexService.LastRequestedWord);
    }

    [Fact]
    public async Task AppendFromAssistantReplyAsync_ShouldReturnResult_EvenWhenIndexServiceThrows()
    {
        var expectedResult = new VocabularyAppendResult(VocabularyAppendStatus.Added);

        var deckService = new FakeVocabularyDeckModeService(expectedResult);
        var indexService = new ThrowingVocabularyIndexService();
        var modeProvider = new FakeStorageModeProvider();

        var sut = new VocabularyPersistenceService(
            deckService,
            indexService,
            modeProvider,
            NullLogger<VocabularyPersistenceService>.Instance);

        var result = await sut.AppendFromAssistantReplyAsync("prepare", "prepare\n\n(v) prepare\n\nWe prepare.");

        Assert.Same(expectedResult, result);
        Assert.Equal(1, deckService.Calls);
    }

    [Fact]
    public async Task AppendFromAssistantReplyAsync_ShouldPassCurrentStorageMode_ToDeckService()
    {
        var expectedResult = new VocabularyAppendResult(VocabularyAppendStatus.Added);
        var deckService = new FakeVocabularyDeckModeService(expectedResult);
        var indexService = new FakeVocabularyIndexService();
        var modeProvider = new FakeStorageModeProvider(VocabularyStorageMode.Local);

        var sut = new VocabularyPersistenceService(
            deckService,
            indexService,
            modeProvider,
            NullLogger<VocabularyPersistenceService>.Instance);

        await sut.AppendFromAssistantReplyAsync("prepare", "prepare\n\n(v) prepare\n\nWe prepare.");

        Assert.Equal(VocabularyStorageMode.Local, deckService.LastMode);
        Assert.Equal(VocabularyStorageMode.Local, indexService.LastMode);
    }

    private sealed class FakeVocabularyDeckModeService : IVocabularyDeckModeService
    {
        private readonly VocabularyAppendResult _result;

        public FakeVocabularyDeckModeService(VocabularyAppendResult result)
        {
            _result = result;
        }

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
            return Task.FromResult(_result);
        }
    }

    private sealed class FakeVocabularyIndexService : IVocabularyIndexService
    {
        public int Calls { get; private set; }

        public VocabularyStorageMode LastMode { get; private set; }

        public string LastRequestedWord { get; private set; } = string.Empty;

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

        public Task HandleAppendResultAsync(string requestedWord, string assistantReply, string? targetDeckFileName, string? overridePartOfSpeech, VocabularyAppendResult appendResult, VocabularyStorageMode storageMode, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastMode = storageMode;
            LastRequestedWord = requestedWord;
            return Task.CompletedTask;
        }

        public Task<int> ClearAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<int> RebuildAsync(IReadOnlyList<VocabularyDeckEntry> entries, VocabularyStorageMode storageMode, CancellationToken cancellationToken = default)
            => Task.FromResult(entries.Count);
    }

    private sealed class FakeStorageModeProvider : IVocabularyStorageModeProvider
    {
        private readonly VocabularyStorageMode _mode;

        public FakeStorageModeProvider(VocabularyStorageMode mode = VocabularyStorageMode.Graph)
        {
            _mode = mode;
        }

        public VocabularyStorageMode CurrentMode => _mode;

        public void SetMode(VocabularyStorageMode mode) { }

        public bool TryParse(string? value, out VocabularyStorageMode mode)
        {
            mode = _mode;
            return true;
        }

        public string ToText(VocabularyStorageMode mode)
            => mode.ToString().ToLowerInvariant();
    }

    private sealed class ThrowingVocabularyIndexService : IVocabularyIndexService
    {
        public Task<VocabularyLookupResult> FindByInputAsync(string input, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Index unavailable");

        public Task<IReadOnlyDictionary<string, VocabularyLookupResult>> FindByInputsAsync(
            IReadOnlyList<string> inputs,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Index unavailable");

        public Task IndexLookupResultAsync(VocabularyLookupResult lookup, VocabularyStorageMode storageMode, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Index unavailable");

        public Task HandleAppendResultAsync(string requestedWord, string assistantReply, string? targetDeckFileName, string? overridePartOfSpeech, VocabularyAppendResult appendResult, VocabularyStorageMode storageMode, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Index unavailable");

        public Task<int> ClearAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Index unavailable");

        public Task<int> RebuildAsync(IReadOnlyList<VocabularyDeckEntry> entries, VocabularyStorageMode storageMode, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Index unavailable");
    }
}


