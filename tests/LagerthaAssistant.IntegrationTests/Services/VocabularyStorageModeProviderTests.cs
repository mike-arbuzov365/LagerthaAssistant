namespace LagerthaAssistant.IntegrationTests.Services;

using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.Infrastructure.Options;
using LagerthaAssistant.Infrastructure.Services.Vocabulary;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public sealed class VocabularyStorageModeProviderTests
{
    [Theory]
    [InlineData("local", VocabularyStorageMode.Local)]
    [InlineData("graph", VocabularyStorageMode.Graph)]
    [InlineData("LOCAL", VocabularyStorageMode.Local)]
    [InlineData("GRAPH", VocabularyStorageMode.Graph)]
    public void TryParse_ShouldParseKnownModes(string value, VocabularyStorageMode expected)
    {
        var provider = new VocabularyStorageModeProvider(new VocabularyStorageOptions { DefaultMode = "local" });

        var parsed = provider.TryParse(value, out var actual);

        Assert.True(parsed);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ToText_ShouldReturnStableModeNames()
    {
        var provider = new VocabularyStorageModeProvider(new VocabularyStorageOptions { DefaultMode = "graph" });

        Assert.Equal("local", provider.ToText(VocabularyStorageMode.Local));
        Assert.Equal("graph", provider.ToText(VocabularyStorageMode.Graph));
    }

    [Fact]
    public async Task SwitchableService_ShouldUseBackendForCurrentMode()
    {
        var provider = new VocabularyStorageModeProvider(new VocabularyStorageOptions { DefaultMode = "local" });

        var localBackend = new FakeBackend(VocabularyStorageMode.Local, "local-deck");
        var graphBackend = new FakeBackend(VocabularyStorageMode.Graph, "graph-deck");

        var sut = new SwitchableVocabularyDeckService(
            [localBackend, graphBackend],
            provider,
            NullLogger<SwitchableVocabularyDeckService>.Instance);

        var localResult = await sut.GetWritableDeckFilesAsync();
        Assert.Single(localResult);
        Assert.Equal("local-deck", localResult[0].FileName);

        provider.SetMode(VocabularyStorageMode.Graph);

        var graphResult = await sut.GetWritableDeckFilesAsync();
        Assert.Single(graphResult);
        Assert.Equal("graph-deck", graphResult[0].FileName);
    }

    private sealed class FakeBackend : IVocabularyDeckBackend
    {
        private readonly IReadOnlyList<VocabularyDeckFile> _files;

        public FakeBackend(VocabularyStorageMode mode, string fileName)
        {
            Mode = mode;
            _files = [new VocabularyDeckFile(fileName, $"/{fileName}")];
        }

        public VocabularyStorageMode Mode { get; }

        public Task<VocabularyLookupResult> FindInWritableDecksAsync(string word, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new VocabularyLookupResult(word, []));
        }

        public Task<IReadOnlyList<VocabularyDeckFile>> GetWritableDeckFilesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_files);
        }

        public Task<VocabularyAppendPreviewResult> PreviewAppendFromAssistantReplyAsync(
            string requestedWord,
            string assistantReply,
            string? forcedDeckFileName = null,
            string? overridePartOfSpeech = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new VocabularyAppendPreviewResult(
                VocabularyAppendPreviewStatus.NoWritableDecks,
                requestedWord,
                Message: "not used in this test"));
        }

        public Task<VocabularyAppendResult> AppendFromAssistantReplyAsync(
            string requestedWord,
            string assistantReply,
            string? forcedDeckFileName = null,
            string? overridePartOfSpeech = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new VocabularyAppendResult(
                VocabularyAppendStatus.NoWritableDecks,
                Message: "not used in this test"));
        }
    }
}
