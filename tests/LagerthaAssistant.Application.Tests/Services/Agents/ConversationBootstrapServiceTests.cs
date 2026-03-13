namespace LagerthaAssistant.Application.Tests.Services.Agents;

using LagerthaAssistant.Application.Interfaces.Agents;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.Application.Services.Agents;
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
        var graphAuthService = new FakeGraphAuthService
        {
            CurrentStatus = new GraphAuthStatus(true, true, "Authenticated.", DateTimeOffset.UtcNow.AddHours(1))
        };

        var sut = new ConversationBootstrapService(
            sessionPreferenceService,
            saveModePreferenceService,
            storageModeProvider,
            graphAuthService);

        var scope = ConversationScope.Create("telegram", "mike", "chat-42");
        var snapshot = await sut.BuildAsync(scope, CancellationToken.None);

        Assert.Equal(scope, snapshot.Scope);
        Assert.Equal("auto", snapshot.SaveMode);
        Assert.Equal(["ask", "auto", "off"], snapshot.AvailableSaveModes);
        Assert.Equal("graph", snapshot.StorageMode);
        Assert.Equal(["local", "graph"], snapshot.AvailableStorageModes);
        Assert.True(snapshot.Graph.IsAuthenticated);
        Assert.NotEmpty(snapshot.CommandGroups);
        Assert.NotEmpty(snapshot.PartOfSpeechOptions);
        Assert.True(snapshot.PartOfSpeechOptions.Zip(snapshot.PartOfSpeechOptions.Skip(1), (a, b) => a.Number <= b.Number).All(x => x));
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
}
