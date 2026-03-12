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
                "(v) ????????",
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
            "prepare\n\n(v) ????????\n\nWe prepare release notes.",
            "wm-verbs-us-en.xlsx",
            "v");

        Assert.Same(expectedResult, result);
        Assert.Equal(1, deckService.Calls);
        Assert.Equal(1, indexService.Calls);
        Assert.Equal(VocabularyStorageMode.Graph, indexService.LastMode);
        Assert.Equal("prepare", indexService.LastRequestedWord);
    }

    private sealed class FakeVocabularyDeckModeService : IVocabularyDeckModeService
    {
        private readonly VocabularyAppendResult _result;

        public FakeVocabularyDeckModeService(VocabularyAppendResult result)
        {
            _result = result;
        }

        public int Calls { get; private set; }

        public Task<VocabularyAppendResult> AppendFromAssistantReplyAsync(
            VocabularyStorageMode mode,
            string requestedWord,
            string assistantReply,
            string? forcedDeckFileName = null,
            string? overridePartOfSpeech = null,
            CancellationToken cancellationToken = default)
        {
            Calls++;
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

        public Task IndexLookupResultAsync(VocabularyLookupResult lookup, VocabularyStorageMode storageMode, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task HandleAppendResultAsync(string requestedWord, string assistantReply, string? targetDeckFileName, string? overridePartOfSpeech, VocabularyAppendResult appendResult, VocabularyStorageMode storageMode, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastMode = storageMode;
            LastRequestedWord = requestedWord;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeStorageModeProvider : IVocabularyStorageModeProvider
    {
        public VocabularyStorageMode CurrentMode => VocabularyStorageMode.Graph;

        public void SetMode(VocabularyStorageMode mode)
        {
        }

        public bool TryParse(string? value, out VocabularyStorageMode mode)
        {
            mode = VocabularyStorageMode.Graph;
            return true;
        }

        public string ToText(VocabularyStorageMode mode)
            => mode.ToString().ToLowerInvariant();
    }
}


