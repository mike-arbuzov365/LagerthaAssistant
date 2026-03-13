namespace LagerthaAssistant.IntegrationTests.Controllers;

using LagerthaAssistant.Api.Contracts;
using LagerthaAssistant.Api.Controllers;
using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Models.Vocabulary;
using Microsoft.AspNetCore.Mvc;
using Xunit;

public sealed class SessionControllerTests
{
    [Fact]
    public async Task GetBootstrap_ShouldReturnCombinedSessionPayload_WithDefaults()
    {
        var scopeAccessor = new FakeConversationScopeAccessor();
        var sessionService = new FakeVocabularySessionPreferenceService
        {
            CurrentSaveMode = VocabularySaveMode.Auto,
            CurrentStorageMode = VocabularyStorageMode.Local
        };
        var saveModeService = new FakeVocabularySaveModePreferenceService();
        var storageModeProvider = new FakeVocabularyStorageModeProvider();
        var graphAuthService = new FakeGraphAuthService
        {
            CurrentStatus = new GraphAuthStatus(true, false, "Not authenticated.", null)
        };

        var sut = new SessionController(
            scopeAccessor,
            sessionService,
            saveModeService,
            storageModeProvider,
            graphAuthService);

        var response = await sut.GetBootstrap(cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<SessionBootstrapResponse>(ok.Value);

        Assert.Equal("api", payload.Scope.Channel);
        Assert.Equal("anonymous", payload.Scope.UserId);
        Assert.Equal("default", payload.Scope.ConversationId);

        Assert.Equal("auto", payload.Preferences.SaveMode);
        Assert.Equal("local", payload.Preferences.StorageMode);
        Assert.Equal(["ask", "auto", "off"], payload.Preferences.AvailableSaveModes);
        Assert.Equal(["local", "graph"], payload.Preferences.AvailableStorageModes);

        Assert.True(payload.Graph.IsConfigured);
        Assert.False(payload.Graph.IsAuthenticated);
        Assert.Equal("Not authenticated.", payload.Graph.Message);

        Assert.NotEmpty(payload.CommandGroups);
        Assert.Contains(payload.CommandGroups, g => g.Category == "Session");
        Assert.NotEmpty(payload.PartOfSpeechOptions);
        Assert.Contains(payload.PartOfSpeechOptions, option => option.Marker == "n");
    }

    [Fact]
    public async Task GetBootstrap_ShouldNormalizeScopeFromQuery()
    {
        var scopeAccessor = new FakeConversationScopeAccessor();
        var sessionService = new FakeVocabularySessionPreferenceService();
        var saveModeService = new FakeVocabularySaveModePreferenceService();
        var storageModeProvider = new FakeVocabularyStorageModeProvider();
        var graphAuthService = new FakeGraphAuthService();

        var sut = new SessionController(
            scopeAccessor,
            sessionService,
            saveModeService,
            storageModeProvider,
            graphAuthService);

        var response = await sut.GetBootstrap(" TeLeGrAm ", "Mike", "chat-42", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<SessionBootstrapResponse>(ok.Value);

        Assert.Equal("telegram", payload.Scope.Channel);
        Assert.Equal("mike", payload.Scope.UserId);
        Assert.Equal("chat-42", payload.Scope.ConversationId);
        Assert.Equal(scopeAccessor.Current, sessionService.LastGetScope);
    }

    private sealed class FakeConversationScopeAccessor : IConversationScopeAccessor
    {
        public ConversationScope Current { get; private set; } = ConversationScope.Default;

        public void Set(ConversationScope scope)
        {
            Current = scope;
        }
    }

    private sealed class FakeVocabularySessionPreferenceService : IVocabularySessionPreferenceService
    {
        public IReadOnlyList<string> SupportedSaveModes { get; } = ["ask", "auto", "off"];

        public IReadOnlyList<string> SupportedStorageModes { get; } = ["local", "graph"];

        public VocabularySaveMode CurrentSaveMode { get; set; } = VocabularySaveMode.Ask;

        public VocabularyStorageMode CurrentStorageMode { get; set; } = VocabularyStorageMode.Local;

        public ConversationScope? LastGetScope { get; private set; }

        public Task<VocabularySessionPreferences> GetAsync(
            ConversationScope scope,
            CancellationToken cancellationToken = default)
        {
            LastGetScope = scope;
            return Task.FromResult(new VocabularySessionPreferences(CurrentSaveMode, CurrentStorageMode));
        }

        public Task<VocabularySessionPreferences> SetAsync(
            ConversationScope scope,
            VocabularySaveMode? saveMode = null,
            VocabularyStorageMode? storageMode = null,
            CancellationToken cancellationToken = default)
        {
            if (saveMode.HasValue)
            {
                CurrentSaveMode = saveMode.Value;
            }

            if (storageMode.HasValue)
            {
                CurrentStorageMode = storageMode.Value;
            }

            return Task.FromResult(new VocabularySessionPreferences(CurrentSaveMode, CurrentStorageMode));
        }
    }

    private sealed class FakeVocabularySaveModePreferenceService : IVocabularySaveModePreferenceService
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

    private sealed class FakeVocabularyStorageModeProvider : IVocabularyStorageModeProvider
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
