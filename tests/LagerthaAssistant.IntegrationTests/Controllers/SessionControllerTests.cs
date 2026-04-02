namespace LagerthaAssistant.IntegrationTests.Controllers;

using LagerthaAssistant.Api.Contracts;
using LagerthaAssistant.Api.Controllers;
using LagerthaAssistant.Application.Interfaces.Agents;
using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Interfaces;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Models.Localization;
using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.Application.Services.Vocabulary;
using Microsoft.AspNetCore.Mvc;
using Xunit;

public sealed class SessionControllerTests
{
    [Fact]
    public async Task GetBootstrap_ShouldReturnCombinedSessionPayload_WithDefaults()
    {
        var scopeAccessor = new FakeConversationScopeAccessor();
        var bootstrapService = new FakeConversationBootstrapService
        {
            SaveMode = "auto",
            StorageMode = "local",
            Graph = new GraphAuthStatus(true, false, "Not authenticated.", null),
            CommandGroups =
            [
                new ConversationCommandCatalogGroup(
                    "Session",
                    [new ConversationCommandCatalogItem("Session", "/help", "Show help")])
            ],
            PartOfSpeechOptions =
            [
                new VocabularyPartOfSpeechOption(1, "n", "noun", ["n", "noun"])
            ]
        };
        var localeStateService = new FakeUserLocaleStateService();

        var sut = new SessionController(scopeAccessor, bootstrapService, localeStateService);

        var response = await sut.GetBootstrap(cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<SessionBootstrapResponse>(ok.Value);

        Assert.Equal("api", payload.Scope.Channel);
        Assert.Equal("anonymous", payload.Scope.UserId);
        Assert.Equal("default", payload.Scope.ConversationId);
        Assert.Equal("uk", payload.Locale.Locale);
        Assert.Equal(["uk", "en"], payload.Locale.AvailableLocales);

        Assert.Equal("auto", payload.Preferences.SaveMode);
        Assert.Equal("local", payload.Preferences.StorageMode);
        Assert.Equal(["ask", "auto", "off"], payload.Preferences.AvailableSaveModes);
        Assert.Equal(["local", "graph"], payload.Preferences.AvailableStorageModes);

        Assert.True(payload.Graph.IsConfigured);
        Assert.False(payload.Graph.IsAuthenticated);
        Assert.Equal("Not authenticated.", payload.Graph.Message);
        Assert.Equal("graph_only_v1", payload.Policy.StorageModePolicy);
        Assert.True(payload.Policy.RequiresInitDataVerification);

        Assert.NotEmpty(payload.CommandGroups);
        Assert.Contains(payload.CommandGroups, g => g.Category == "Session");
        Assert.NotEmpty(payload.PartOfSpeechOptions);
        Assert.Contains(payload.PartOfSpeechOptions, option => option.Marker == "n");
        Assert.Null(payload.WritableDecks);
    }

    [Fact]
    public async Task GetBootstrap_ShouldNormalizeScopeFromQuery()
    {
        var scopeAccessor = new FakeConversationScopeAccessor();
        var bootstrapService = new FakeConversationBootstrapService();
        var localeStateService = new FakeUserLocaleStateService();

        var sut = new SessionController(scopeAccessor, bootstrapService, localeStateService);

        var response = await sut.GetBootstrap(
            " TeLeGrAm ",
            "Mike",
            "chat-42",
            includeCommands: true,
            includePartOfSpeechOptions: true,
            includeDecks: true,
            cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<SessionBootstrapResponse>(ok.Value);

        Assert.Equal("telegram", payload.Scope.Channel);
        Assert.Equal("mike", payload.Scope.UserId);
        Assert.Equal("chat-42", payload.Scope.ConversationId);
        Assert.Equal(scopeAccessor.Current, bootstrapService.LastScope);
        Assert.NotNull(bootstrapService.LastOptions);
        Assert.True(bootstrapService.LastOptions!.IncludeWritableDecks);
    }

    [Fact]
    public async Task GetBootstrap_ShouldAllowDisablingOptionalCollections()
    {
        var scopeAccessor = new FakeConversationScopeAccessor();
        var bootstrapService = new FakeConversationBootstrapService
        {
            CommandGroups =
            [
                new ConversationCommandCatalogGroup(
                    "Session",
                    [new ConversationCommandCatalogItem("Session", "/help", "Show help")])
            ],
            PartOfSpeechOptions =
            [
                new VocabularyPartOfSpeechOption(1, "n", "noun", ["n", "noun"])
            ]
        };
        var localeStateService = new FakeUserLocaleStateService();

        var sut = new SessionController(scopeAccessor, bootstrapService, localeStateService);

        var response = await sut.GetBootstrap(
            includeCommands: false,
            includePartOfSpeechOptions: false,
            includeDecks: false,
            cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<SessionBootstrapResponse>(ok.Value);

        Assert.Empty(payload.CommandGroups);
        Assert.Empty(payload.PartOfSpeechOptions);
        Assert.NotNull(bootstrapService.LastOptions);
        Assert.False(bootstrapService.LastOptions!.IncludeCommandGroups);
        Assert.False(bootstrapService.LastOptions!.IncludePartOfSpeechOptions);
        Assert.False(bootstrapService.LastOptions!.IncludeWritableDecks);
    }

    [Fact]
    public async Task GetBootstrap_ShouldMapWritableDecks_WhenIncluded()
    {
        var scopeAccessor = new FakeConversationScopeAccessor();
        var bootstrapService = new FakeConversationBootstrapService
        {
            WritableDecks =
            [
                new VocabularyDeckFile("wm-verbs-us-en.xlsx", "C:\\Decks\\wm-verbs-us-en.xlsx"),
                new VocabularyDeckFile("wm-nouns-ua-en.xlsx", "C:\\Decks\\wm-nouns-ua-en.xlsx")
            ]
        };
        var localeStateService = new FakeUserLocaleStateService
        {
            StoredLocale = "en-US",
        };

        var sut = new SessionController(scopeAccessor, bootstrapService, localeStateService);

        var response = await sut.GetBootstrap(
            includeDecks: true,
            cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<SessionBootstrapResponse>(ok.Value);

        var decks = Assert.IsType<List<VocabularyDeckInfoResponse>>(payload.WritableDecks);
        Assert.Equal(2, decks.Count);
        Assert.Equal("wm-nouns-ua-en.xlsx", decks[0].FileName);
        Assert.Equal("n", decks[0].SuggestedPartOfSpeech);
        Assert.Equal("wm-verbs-us-en.xlsx", decks[1].FileName);
        Assert.Equal("v", decks[1].SuggestedPartOfSpeech);
        Assert.Equal("en", payload.Locale.Locale);
    }

    [Fact]
    public async Task GetBootstrap_ShouldReadStoredLocaleAfterBootstrapCompletes_WhenServicesShareScopedState()
    {
        var guard = new SessionBootstrapGuard();
        var scopeAccessor = new FakeConversationScopeAccessor();
        var bootstrapService = new GuardedConversationBootstrapService(guard);
        var localeStateService = new GuardedUserLocaleStateService(guard)
        {
            StoredLocale = "uk"
        };

        var sut = new SessionController(scopeAccessor, bootstrapService, localeStateService);

        var response = await sut.GetBootstrap(cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<SessionBootstrapResponse>(ok.Value);

        Assert.Equal("uk", payload.Locale.Locale);
        Assert.Equal(["bootstrap:start", "bootstrap:end", "locale:start", "locale:end"], guard.Events);
    }

    private sealed class FakeConversationScopeAccessor : IConversationScopeAccessor
    {
        public ConversationScope Current { get; private set; } = ConversationScope.Default;

        public void Set(ConversationScope scope)
        {
            Current = scope;
        }
    }

    private sealed class FakeConversationBootstrapService : IConversationBootstrapService
    {
        public string SaveMode { get; set; } = "ask";

        public IReadOnlyList<string> AvailableSaveModes { get; set; } = ["ask", "auto", "off"];

        public string StorageMode { get; set; } = "local";

        public IReadOnlyList<string> AvailableStorageModes { get; set; } = ["local", "graph"];

        public GraphAuthStatus Graph { get; set; } = new(true, false, "Not authenticated.", null);

        public IReadOnlyList<ConversationCommandCatalogGroup> CommandGroups { get; set; } = [];

        public IReadOnlyList<VocabularyPartOfSpeechOption> PartOfSpeechOptions { get; set; } = [];

        public IReadOnlyList<VocabularyDeckFile> WritableDecks { get; set; } = [];

        public ConversationScope? LastScope { get; private set; }
        public ConversationBootstrapOptions? LastOptions { get; private set; }

        public Task<ConversationBootstrapSnapshot> BuildAsync(
            ConversationScope scope,
            ConversationBootstrapOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            options ??= ConversationBootstrapOptions.Default;
            LastScope = scope;
            LastOptions = options;

            var commandGroups = options.IncludeCommandGroups
                ? CommandGroups
                : [];

            var partOfSpeechOptions = options.IncludePartOfSpeechOptions
                ? PartOfSpeechOptions
                : [];

            return Task.FromResult(new ConversationBootstrapSnapshot(
                scope,
                SaveMode,
                AvailableSaveModes,
                StorageMode,
                AvailableStorageModes,
                Graph,
                commandGroups,
                partOfSpeechOptions,
                options.IncludeWritableDecks
                    ? WritableDecks
                    : null));
        }
    }

    private sealed class FakeUserLocaleStateService : IUserLocaleStateService
    {
        public string? StoredLocale { get; set; }

        public Task<string?> GetStoredLocaleAsync(
            string channel,
            string userId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(StoredLocale);
        }

        public Task<string> SetLocaleAsync(
            string channel,
            string userId,
            string locale,
            bool selectedManually = true,
            CancellationToken cancellationToken = default)
        {
            StoredLocale = locale;
            return Task.FromResult(locale);
        }

        public Task<UserLocaleStateResult> EnsureLocaleAsync(
            string channel,
            string userId,
            string? telegramLanguageCode,
            string? incomingText,
            CancellationToken cancellationToken = default)
        {
            var locale = StoredLocale ?? "uk";
            return Task.FromResult(new UserLocaleStateResult(locale, IsInitialized: true, IsSwitched: false));
        }
    }

    private sealed class SessionBootstrapGuard
    {
        public bool BootstrapCompleted { get; private set; }

        public List<string> Events { get; } = [];

        public void BootstrapStarted() => Events.Add("bootstrap:start");

        public void BootstrapFinished()
        {
            BootstrapCompleted = true;
            Events.Add("bootstrap:end");
        }

        public void LocaleStarted()
        {
            Events.Add("locale:start");
            if (!BootstrapCompleted)
            {
                throw new InvalidOperationException("Stored locale started before bootstrap completed.");
            }
        }

        public void LocaleFinished() => Events.Add("locale:end");
    }

    private sealed class GuardedConversationBootstrapService : IConversationBootstrapService
    {
        private readonly SessionBootstrapGuard _guard;

        public GuardedConversationBootstrapService(SessionBootstrapGuard guard)
        {
            _guard = guard;
        }

        public async Task<ConversationBootstrapSnapshot> BuildAsync(
            ConversationScope scope,
            ConversationBootstrapOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            _guard.BootstrapStarted();
            await Task.Yield();
            _guard.BootstrapFinished();

            return new ConversationBootstrapSnapshot(
                scope,
                "ask",
                ["ask", "auto", "off"],
                "graph",
                ["local", "graph"],
                new GraphAuthStatus(true, false, "Not authenticated.", null),
                [],
                [],
                null);
        }
    }

    private sealed class GuardedUserLocaleStateService : IUserLocaleStateService
    {
        private readonly SessionBootstrapGuard _guard;

        public GuardedUserLocaleStateService(SessionBootstrapGuard guard)
        {
            _guard = guard;
        }

        public string? StoredLocale { get; set; }

        public Task<string?> GetStoredLocaleAsync(
            string channel,
            string userId,
            CancellationToken cancellationToken = default)
        {
            _guard.LocaleStarted();
            _guard.LocaleFinished();
            return Task.FromResult(StoredLocale);
        }

        public Task<string> SetLocaleAsync(
            string channel,
            string userId,
            string locale,
            bool selectedManually = true,
            CancellationToken cancellationToken = default)
        {
            StoredLocale = locale;
            return Task.FromResult(locale);
        }

        public Task<UserLocaleStateResult> EnsureLocaleAsync(
            string channel,
            string userId,
            string? telegramLanguageCode,
            string? incomingText,
            CancellationToken cancellationToken = default)
        {
            var locale = StoredLocale ?? "uk";
            return Task.FromResult(new UserLocaleStateResult(locale, IsInitialized: true, IsSwitched: false));
        }
    }
}

