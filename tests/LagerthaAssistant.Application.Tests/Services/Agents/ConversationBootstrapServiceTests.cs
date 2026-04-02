namespace LagerthaAssistant.Application.Tests.Services.Agents;

using LagerthaAssistant.Application.Interfaces.Agents;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.Application.Services.Agents;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public sealed class ConversationBootstrapServiceTests
{
    [Fact]
    public async Task BuildAsync_ShouldReturnCombinedSnapshot_ForScope()
    {
        var sessionPreferenceService = new FakeSessionPreferenceService
        {
            CurrentSession = new VocabularySessionPreferences(VocabularySaveMode.Auto, VocabularyStorageMode.Graph)
        };
        var saveModePreferenceService = new FakeSaveModePreferenceService();
        var storageModeProvider = new FakeStorageModeProvider();
        var deckService = new FakeDeckService
        {
            WritableDecks =
            [
                new VocabularyDeckFile("wm-verbs-us-en.xlsx", "C:\\Decks\\wm-verbs-us-en.xlsx")
            ]
        };
        var graphAuthService = new FakeGraphAuthService
        {
            CurrentStatus = new GraphAuthStatus(true, true, "Authenticated.", DateTimeOffset.UtcNow.AddHours(1))
        };

        var sut = new ConversationBootstrapService(
            sessionPreferenceService,
            saveModePreferenceService,
            storageModeProvider,
            deckService,
            graphAuthService,
            new ConversationCommandCatalogService(),
            NullLogger<ConversationBootstrapService>.Instance);

        var scope = ConversationScope.Create("telegram", "mike", "chat-42");
        var snapshot = await sut.BuildAsync(
            scope,
            new ConversationBootstrapOptions(
                IncludeCommandGroups: true,
                IncludePartOfSpeechOptions: true,
                IncludeWritableDecks: true),
            CancellationToken.None);

        Assert.Equal(scope, snapshot.Scope);
        Assert.Equal("auto", snapshot.SaveMode);
        Assert.Equal(["ask", "auto", "off"], snapshot.AvailableSaveModes);
        Assert.Equal("graph", snapshot.StorageMode);
        Assert.Equal(["local", "graph"], snapshot.AvailableStorageModes);
        Assert.True(snapshot.Graph.IsAuthenticated);
        Assert.NotEmpty(snapshot.CommandGroups);
        Assert.NotEmpty(snapshot.PartOfSpeechOptions);
        Assert.True(snapshot.PartOfSpeechOptions.Zip(snapshot.PartOfSpeechOptions.Skip(1), (a, b) => a.Number <= b.Number).All(x => x));
        Assert.NotNull(snapshot.WritableDecks);
        Assert.Single(snapshot.WritableDecks!);
        Assert.Equal(1, deckService.GetWritableDeckFilesCalls);
    }

    [Fact]
    public async Task BuildAsync_ShouldSkipDeckEnumeration_WhenIncludeDecksIsFalse()
    {
        var sessionPreferenceService = new FakeSessionPreferenceService();
        var saveModePreferenceService = new FakeSaveModePreferenceService();
        var storageModeProvider = new FakeStorageModeProvider();
        var deckService = new FakeDeckService();
        var graphAuthService = new FakeGraphAuthService();

        var sut = new ConversationBootstrapService(
            sessionPreferenceService,
            saveModePreferenceService,
            storageModeProvider,
            deckService,
            graphAuthService,
            new ConversationCommandCatalogService(),
            NullLogger<ConversationBootstrapService>.Instance);

        var snapshot = await sut.BuildAsync(
            ConversationScope.Default,
            new ConversationBootstrapOptions(
                IncludeCommandGroups: true,
                IncludePartOfSpeechOptions: true,
                IncludeWritableDecks: false),
            CancellationToken.None);

        Assert.Null(snapshot.WritableDecks);
        Assert.Equal(0, deckService.GetWritableDeckFilesCalls);
    }

    [Fact]
    public async Task BuildAsync_ShouldSupportDisablingCommandAndPartOfSpeechCollections()
    {
        var sessionPreferenceService = new FakeSessionPreferenceService();
        var saveModePreferenceService = new FakeSaveModePreferenceService();
        var storageModeProvider = new FakeStorageModeProvider();
        var deckService = new FakeDeckService();
        var graphAuthService = new FakeGraphAuthService();

        var sut = new ConversationBootstrapService(
            sessionPreferenceService,
            saveModePreferenceService,
            storageModeProvider,
            deckService,
            graphAuthService,
            new ConversationCommandCatalogService(),
            NullLogger<ConversationBootstrapService>.Instance);

        var snapshot = await sut.BuildAsync(
            ConversationScope.Default,
            new ConversationBootstrapOptions(
                IncludeCommandGroups: false,
                IncludePartOfSpeechOptions: false,
                IncludeWritableDecks: false),
            CancellationToken.None);

        Assert.Empty(snapshot.CommandGroups);
        Assert.Empty(snapshot.PartOfSpeechOptions);
    }

    [Fact]
    public async Task BuildAsync_ShouldReadSessionBeforeGraphStatus_WhenServicesCannotRunConcurrently()
    {
        var guard = new SequentialBootstrapGuard();
        var sessionPreferenceService = new GuardedSessionPreferenceService(guard);
        var saveModePreferenceService = new FakeSaveModePreferenceService();
        var storageModeProvider = new FakeStorageModeProvider();
        var deckService = new FakeDeckService();
        var graphAuthService = new GuardedGraphAuthService(guard);

        var sut = new ConversationBootstrapService(
            sessionPreferenceService,
            saveModePreferenceService,
            storageModeProvider,
            deckService,
            graphAuthService,
            new ConversationCommandCatalogService(),
            NullLogger<ConversationBootstrapService>.Instance);

        var snapshot = await sut.BuildAsync(ConversationScope.Default, cancellationToken: CancellationToken.None);

        Assert.Equal("ask", snapshot.SaveMode);
        Assert.False(snapshot.Graph.IsAuthenticated);
        Assert.Equal(["session:start", "session:end", "graph:start", "graph:end"], guard.Events);
    }

    [Fact]
    public async Task BuildAsync_ShouldDegradeGracefully_WhenGraphStatusThrows()
    {
        var sut = new ConversationBootstrapService(
            new FakeSessionPreferenceService(),
            new FakeSaveModePreferenceService(),
            new FakeStorageModeProvider(),
            new FakeDeckService(),
            new ThrowingGraphAuthService(),
            new ConversationCommandCatalogService(),
            NullLogger<ConversationBootstrapService>.Instance);

        var snapshot = await sut.BuildAsync(ConversationScope.Default, cancellationToken: CancellationToken.None);

        Assert.True(snapshot.Graph.IsConfigured);
        Assert.False(snapshot.Graph.IsAuthenticated);
        Assert.Equal("Graph status unavailable. Retry later.", snapshot.Graph.Message);
    }
    private sealed class FakeSessionPreferenceService : IVocabularySessionPreferenceService
    {
        public IReadOnlyList<string> SupportedSaveModes { get; } = ["ask", "auto", "off"];

        public IReadOnlyList<string> SupportedStorageModes { get; } = ["local", "graph"];

        public VocabularySessionPreferences CurrentSession { get; set; } = new(VocabularySaveMode.Ask, VocabularyStorageMode.Local);

        public Task<VocabularySessionPreferences> GetAsync(
            ConversationScope scope,
            CancellationToken cancellationToken = default)
            => Task.FromResult(CurrentSession);

        public Task<VocabularySessionPreferences> SetAsync(
            ConversationScope scope,
            VocabularySaveMode? saveMode = null,
            VocabularyStorageMode? storageMode = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(CurrentSession);
    }

    private sealed class FakeSaveModePreferenceService : IVocabularySaveModePreferenceService
    {
        public IReadOnlyList<string> SupportedModes { get; } = ["ask", "auto", "off"];

        public bool TryParse(string? value, out VocabularySaveMode mode)
        {
            mode = VocabularySaveMode.Ask;
            return true;
        }

        public string ToText(VocabularySaveMode mode) => mode.ToString().ToLowerInvariant();

        public Task<VocabularySaveMode> GetModeAsync(ConversationScope scope, CancellationToken cancellationToken = default)
            => Task.FromResult(VocabularySaveMode.Ask);

        public Task<VocabularySaveMode> SetModeAsync(
            ConversationScope scope,
            VocabularySaveMode mode,
            CancellationToken cancellationToken = default)
            => Task.FromResult(mode);
    }

    private sealed class FakeStorageModeProvider : IVocabularyStorageModeProvider
    {
        public VocabularyStorageMode CurrentMode { get; private set; } = VocabularyStorageMode.Local;

        public void SetMode(VocabularyStorageMode mode)
        {
            CurrentMode = mode;
        }

        public bool TryParse(string? value, out VocabularyStorageMode mode)
        {
            mode = VocabularyStorageMode.Local;
            return true;
        }

        public string ToText(VocabularyStorageMode mode) => mode.ToString().ToLowerInvariant();
    }

    private sealed class FakeGraphAuthService : IGraphAuthService
    {
        public GraphAuthStatus CurrentStatus { get; set; } = new(true, false, "Not authenticated.", null);

        public Task<GraphAuthStatus> GetStatusAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CurrentStatus);

        public Task<GraphLoginResult> LoginAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new GraphLoginResult(false, "Not implemented."));

        public Task<GraphLoginResult> LoginAsync(
            Func<GraphDeviceCodePrompt, CancellationToken, Task> onDeviceCodeReceived,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new GraphLoginResult(false, "Not implemented."));

        public Task<GraphDeviceLoginStartResult> StartLoginAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new GraphDeviceLoginStartResult(false, "Not implemented.", null));

        public Task<GraphLoginResult> CompleteLoginAsync(
            GraphDeviceLoginChallenge challenge,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new GraphLoginResult(false, "Not implemented."));

        public Task LogoutAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);
    }

    private sealed class FakeDeckService : IVocabularyDeckService
    {
        public IReadOnlyList<VocabularyDeckFile> WritableDecks { get; set; } = [];

        public int GetWritableDeckFilesCalls { get; private set; }

        public Task<VocabularyLookupResult> FindInWritableDecksAsync(string word, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new VocabularyLookupResult(word, []));
        }

        public Task<IReadOnlyList<VocabularyDeckFile>> GetWritableDeckFilesAsync(CancellationToken cancellationToken = default)
        {
            GetWritableDeckFilesCalls++;
            return Task.FromResult(WritableDecks);
        }

        public Task<VocabularyAppendPreviewResult> PreviewAppendFromAssistantReplyAsync(
            string requestedWord,
            string assistantReply,
            string? forcedDeckFileName = null,
            string? overridePartOfSpeech = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new VocabularyAppendPreviewResult(
                VocabularyAppendPreviewStatus.Error,
                requestedWord,
                null,
                null,
                null,
                "not implemented"));
        }

        public Task<VocabularyAppendResult> AppendFromAssistantReplyAsync(
            string requestedWord,
            string assistantReply,
            string? forcedDeckFileName = null,
            string? overridePartOfSpeech = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new VocabularyAppendResult(
                VocabularyAppendStatus.Error,
                null,
                null,
                "not implemented"));
        }

        public Task<IReadOnlyList<VocabularyDeckEntry>> GetAllEntriesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<VocabularyDeckEntry>>([]);
    }

    private sealed class SequentialBootstrapGuard
    {
        public bool SessionCompleted { get; private set; }

        public List<string> Events { get; } = [];

        public void SessionStarted() => Events.Add("session:start");

        public void SessionFinished()
        {
            SessionCompleted = true;
            Events.Add("session:end");
        }

        public void GraphStarted()
        {
            Events.Add("graph:start");
            if (!SessionCompleted)
            {
                throw new InvalidOperationException("Graph status started before session preference read completed.");
            }
        }

        public void GraphFinished() => Events.Add("graph:end");
    }

    private sealed class GuardedSessionPreferenceService : IVocabularySessionPreferenceService
    {
        private readonly SequentialBootstrapGuard _guard;

        public GuardedSessionPreferenceService(SequentialBootstrapGuard guard)
        {
            _guard = guard;
        }

        public IReadOnlyList<string> SupportedSaveModes { get; } = ["ask", "auto", "off"];

        public IReadOnlyList<string> SupportedStorageModes { get; } = ["local", "graph"];

        public Task<VocabularySessionPreferences> GetAsync(
            ConversationScope scope,
            CancellationToken cancellationToken = default)
        {
            _guard.SessionStarted();
            _guard.SessionFinished();
            return Task.FromResult(new VocabularySessionPreferences(VocabularySaveMode.Ask, VocabularyStorageMode.Local));
        }

        public Task<VocabularySessionPreferences> SetAsync(
            ConversationScope scope,
            VocabularySaveMode? saveMode = null,
            VocabularyStorageMode? storageMode = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new VocabularySessionPreferences(VocabularySaveMode.Ask, VocabularyStorageMode.Local));
    }

    private sealed class GuardedGraphAuthService : IGraphAuthService
    {
        private readonly SequentialBootstrapGuard _guard;

        public GuardedGraphAuthService(SequentialBootstrapGuard guard)
        {
            _guard = guard;
        }

        public Task<GraphAuthStatus> GetStatusAsync(CancellationToken cancellationToken = default)
        {
            _guard.GraphStarted();
            _guard.GraphFinished();
            return Task.FromResult(new GraphAuthStatus(true, false, "Not authenticated.", null));
        }

        public Task<GraphLoginResult> LoginAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new GraphLoginResult(false, "Not implemented."));

        public Task<GraphLoginResult> LoginAsync(
            Func<GraphDeviceCodePrompt, CancellationToken, Task> onDeviceCodeReceived,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new GraphLoginResult(false, "Not implemented."));

        public Task<GraphDeviceLoginStartResult> StartLoginAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new GraphDeviceLoginStartResult(false, "Not implemented.", null));

        public Task<GraphLoginResult> CompleteLoginAsync(
            GraphDeviceLoginChallenge challenge,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new GraphLoginResult(false, "Not implemented."));

        public Task LogoutAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);
    }
    private sealed class ThrowingGraphAuthService : IGraphAuthService
    {
        public Task<GraphAuthStatus> GetStatusAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("temporary Graph failure");

        public Task<GraphLoginResult> LoginAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new GraphLoginResult(false, "Not implemented."));

        public Task<GraphLoginResult> LoginAsync(
            Func<GraphDeviceCodePrompt, CancellationToken, Task> onDeviceCodeReceived,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new GraphLoginResult(false, "Not implemented."));

        public Task<GraphDeviceLoginStartResult> StartLoginAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new GraphDeviceLoginStartResult(false, "Not implemented.", null));

        public Task<GraphLoginResult> CompleteLoginAsync(
            GraphDeviceLoginChallenge challenge,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new GraphLoginResult(false, "Not implemented."));

        public Task LogoutAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);
    }
}
