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
        var resolver = CreateResolver(localBackend, graphBackend);

        var sut = new SwitchableVocabularyDeckService(
            resolver,
            provider);

        var localResult = await sut.GetWritableDeckFilesAsync();
        Assert.Single(localResult);
        Assert.Equal("local-deck", localResult[0].FileName);

        provider.SetMode(VocabularyStorageMode.Graph);

        var graphResult = await sut.GetWritableDeckFilesAsync();
        Assert.Single(graphResult);
        Assert.Equal("graph-deck", graphResult[0].FileName);
    }

    [Fact]
    public async Task SwitchableService_ShouldFallbackToLocal_WhenCurrentModeBackendMissing()
    {
        var provider = new VocabularyStorageModeProvider(new VocabularyStorageOptions { DefaultMode = "graph" });
        var localBackend = new FakeBackend(VocabularyStorageMode.Local, "local-deck");
        var resolver = CreateResolver(localBackend);

        var sut = new SwitchableVocabularyDeckService(
            resolver,
            provider);

        var result = await sut.GetWritableDeckFilesAsync();

        Assert.Single(result);
        Assert.Equal("local-deck", result[0].FileName);
    }

    [Fact]
    public async Task DeckModeService_ShouldUseRequestedModeBackend()
    {
        var localBackend = new FakeBackend(VocabularyStorageMode.Local, "local-deck");
        var graphBackend = new FakeBackend(VocabularyStorageMode.Graph, "graph-deck");
        var resolver = CreateResolver(localBackend, graphBackend);
        var sut = new VocabularyDeckModeService(resolver);

        var result = await sut.AppendFromAssistantReplyAsync(
            VocabularyStorageMode.Graph,
            "void",
            "void\n\n(n) emptiness");

        Assert.Equal(VocabularyAppendStatus.Added, result.Status);
        Assert.Equal(0, localBackend.AppendCalls);
        Assert.Equal(1, graphBackend.AppendCalls);
    }

    [Fact]
    public async Task DeckModeService_ShouldFallbackToLocal_WhenRequestedModeBackendMissing()
    {
        var localBackend = new FakeBackend(VocabularyStorageMode.Local, "local-deck");
        var resolver = CreateResolver(localBackend);
        var sut = new VocabularyDeckModeService(resolver);

        var result = await sut.AppendFromAssistantReplyAsync(
            VocabularyStorageMode.Graph,
            "void",
            "void\n\n(n) emptiness");

        Assert.Equal(VocabularyAppendStatus.Added, result.Status);
        Assert.Equal(1, localBackend.AppendCalls);
    }

    [Fact]
    public void BackendResolver_ShouldThrow_WhenNoBackendsRegistered()
    {
        var resolver = new VocabularyDeckBackendResolver([], NullLogger<VocabularyDeckBackendResolver>.Instance);

        var act = () => resolver.Resolve(VocabularyStorageMode.Local);

        var exception = Assert.Throws<InvalidOperationException>(act);
        Assert.Equal("No vocabulary backends are registered.", exception.Message);
    }

    private static VocabularyDeckBackendResolver CreateResolver(params IVocabularyDeckBackend[] backends)
    {
        return new VocabularyDeckBackendResolver(
            backends,
            NullLogger<VocabularyDeckBackendResolver>.Instance);
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

        public int AppendCalls { get; private set; }

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
            AppendCalls++;

            return Task.FromResult(new VocabularyAppendResult(
                VocabularyAppendStatus.Added,
                Entry: new VocabularyDeckEntry(
                    _files[0].FileName,
                    _files[0].FullPath,
                    1,
                    requestedWord,
                    "(n) test",
                    "Example sentence.")));
        }

        public Task<IReadOnlyList<VocabularyDeckEntry>> GetAllEntriesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<VocabularyDeckEntry>>([]);
    }
}
