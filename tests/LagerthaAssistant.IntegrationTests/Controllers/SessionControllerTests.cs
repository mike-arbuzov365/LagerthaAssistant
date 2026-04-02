namespace LagerthaAssistant.IntegrationTests.Controllers;

using System.Security.Cryptography;
using System.Text;
using LagerthaAssistant.Api.Contracts;
using LagerthaAssistant.Api.Controllers;
using LagerthaAssistant.Api.Options;
using LagerthaAssistant.Application.Interfaces;
using LagerthaAssistant.Application.Interfaces.Agents;
using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Interfaces.Food;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Models.Food;
using LagerthaAssistant.Application.Models.Localization;
using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.Application.Services.Vocabulary;
using LagerthaAssistant.Infrastructure.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SharedBotKernel.Infrastructure.AI;
using SharedBotKernel.Models.AI;
using Xunit;

public sealed class SessionControllerTests
{
    private const string BotToken = "123456:ABCDEF_fake_token_for_tests";

    [Fact]
    public async Task GetBootstrap_ShouldReturnCombinedSessionPayload_WithSettingsSnapshot()
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
        var aiRuntimeSettings = new FakeAiRuntimeSettingsService();
        var notionSyncProcessor = new FakeNotionSyncProcessor();
        var foodSyncService = new FakeFoodSyncService();

        var sut = CreateSut(
            scopeAccessor,
            bootstrapService,
            localeStateService,
            aiRuntimeSettings,
            notionSyncProcessor,
            foodSyncService);

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

        Assert.Equal("openai", payload.Settings.AiProvider);
        Assert.Equal("gpt-4.1-mini", payload.Settings.AiModel);
        Assert.Equal(["openai", "claude"], payload.Settings.AvailableProviders);
        Assert.Equal(["gpt-4.1-mini", "gpt-4.1"], payload.Settings.AvailableModels);
        Assert.False(payload.Settings.HasStoredKey);
        Assert.Equal("missing", payload.Settings.ApiKeySource);
        Assert.False(payload.Settings.Notion.NotionVocabulary.Enabled);
        Assert.False(payload.Settings.Notion.NotionFood.Enabled);

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

        var sut = CreateSut(
            scopeAccessor,
            bootstrapService,
            new FakeUserLocaleStateService(),
            new FakeAiRuntimeSettingsService(),
            new FakeNotionSyncProcessor(),
            new FakeFoodSyncService());

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

        var sut = CreateSut(
            scopeAccessor,
            bootstrapService,
            new FakeUserLocaleStateService(),
            new FakeAiRuntimeSettingsService(),
            new FakeNotionSyncProcessor(),
            new FakeFoodSyncService());

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

        var sut = CreateSut(
            scopeAccessor,
            bootstrapService,
            localeStateService,
            new FakeAiRuntimeSettingsService(),
            new FakeNotionSyncProcessor(),
            new FakeFoodSyncService());

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

        var sut = CreateSut(
            scopeAccessor,
            bootstrapService,
            localeStateService,
            new FakeAiRuntimeSettingsService(),
            new FakeNotionSyncProcessor(),
            new FakeFoodSyncService());

        var response = await sut.GetBootstrap(cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<SessionBootstrapResponse>(ok.Value);

        Assert.Equal("uk", payload.Locale.Locale);
        Assert.Equal(["bootstrap:start", "bootstrap:end", "locale:start", "locale:end"], guard.Events);
    }

    [Fact]
    public async Task PostBootstrap_ShouldResolveVerifiedTelegramScopeFromInitData()
    {
        var scopeAccessor = new FakeConversationScopeAccessor();
        var bootstrapService = new FakeConversationBootstrapService();
        var initData = BuildInitData(
            BotToken,
            DateTimeOffset.UtcNow.AddMinutes(-1),
            ("query_id", "AAHx"),
            ("user", "{\"id\":2002,\"first_name\":\"Mike\"}"));

        var sut = CreateSut(
            scopeAccessor,
            bootstrapService,
            new FakeUserLocaleStateService(),
            new FakeAiRuntimeSettingsService(),
            new FakeNotionSyncProcessor(),
            new FakeFoodSyncService(),
            telegramOptions: new TelegramOptions { BotToken = BotToken });

        var response = await sut.PostBootstrap(
            new SessionBootstrapRequest(
                Channel: "telegram",
                UserId: "9999",
                ConversationId: "2002:17",
                InitData: initData),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<SessionBootstrapResponse>(ok.Value);

        Assert.Equal("telegram", payload.Scope.Channel);
        Assert.Equal("2002", payload.Scope.UserId);
        Assert.Equal("2002:17", payload.Scope.ConversationId);
        Assert.Equal("2002", bootstrapService.LastScope?.UserId);
        Assert.Equal("2002:17", bootstrapService.LastScope?.ConversationId);
    }

    [Fact]
    public async Task GetBootstrap_ShouldFallbackToSafeSettingsSnapshot_WhenSettingsEnrichmentFails()
    {
        var sut = CreateSut(
            new FakeConversationScopeAccessor(),
            new FakeConversationBootstrapService(),
            new FakeUserLocaleStateService(),
            new ThrowingAiRuntimeSettingsService(),
            new ThrowingNotionSyncProcessor(),
            new ThrowingFoodSyncService(),
            notionFoodOptions: new NotionFoodOptions { Enabled = true },
            notionSyncWorkerOptions: new NotionSyncWorkerOptions { Enabled = true },
            foodSyncWorkerOptions: new FoodSyncWorkerOptions { Enabled = true });

        var response = await sut.GetBootstrap(cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<SessionBootstrapResponse>(ok.Value);

        Assert.Equal("openai", payload.Settings.AiProvider);
        Assert.Equal("gpt-4.1-mini", payload.Settings.AiModel);
        Assert.False(payload.Settings.HasStoredKey);
        Assert.Equal("missing", payload.Settings.ApiKeySource);
        Assert.Equal("Unavailable", payload.Settings.Notion.NotionVocabulary.Message);
        Assert.True(payload.Settings.Notion.NotionVocabulary.WorkerEnabled);
        Assert.True(payload.Settings.Notion.NotionFood.WorkerEnabled);
    }

    private static SessionController CreateSut(
        IConversationScopeAccessor scopeAccessor,
        IConversationBootstrapService bootstrapService,
        IUserLocaleStateService localeStateService,
        IAiRuntimeSettingsService aiRuntimeSettingsService,
        INotionSyncProcessor notionSyncProcessor,
        IFoodSyncService foodSyncService,
        TelegramOptions? telegramOptions = null,
        NotionFoodOptions? notionFoodOptions = null,
        NotionSyncWorkerOptions? notionSyncWorkerOptions = null,
        FoodSyncWorkerOptions? foodSyncWorkerOptions = null)
    {
        return new SessionController(
            scopeAccessor,
            bootstrapService,
            localeStateService,
            aiRuntimeSettingsService,
            notionSyncProcessor,
            foodSyncService,
            Options.Create(telegramOptions ?? new TelegramOptions()),
            notionFoodOptions ?? new NotionFoodOptions(),
            Options.Create(notionSyncWorkerOptions ?? new NotionSyncWorkerOptions()),
            Options.Create(foodSyncWorkerOptions ?? new FoodSyncWorkerOptions()));
    }

    private static string BuildInitData(
        string botToken,
        DateTimeOffset authDateUtc,
        params (string Key, string Value)[] fields)
    {
        var parameters = fields.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        parameters["auth_date"] = authDateUtc.ToUnixTimeSeconds().ToString();

        var dataCheckString = string.Join(
            '\n',
            parameters
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .Select(x => $"{x.Key}={x.Value}"));

        var secret = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes("WebAppData"),
            Encoding.UTF8.GetBytes(botToken));
        var hash = HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(dataCheckString));
        parameters["hash"] = Convert.ToHexStringLower(hash);

        return string.Join(
            "&",
            parameters.Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value)}"));
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

    private class FakeAiRuntimeSettingsService : IAiRuntimeSettingsService
    {
        public IReadOnlyList<string> SupportedProviders => ["openai", "claude"];

        public bool TryNormalizeProvider(string? value, out string provider)
        {
            var normalized = value?.Trim().ToLowerInvariant();
            if (normalized is "openai" or "claude")
            {
                provider = normalized;
                return true;
            }

            provider = "openai";
            return false;
        }

        public IReadOnlyList<string> GetSupportedModels(string provider)
            => string.Equals(provider, "claude", StringComparison.Ordinal)
                ? ["claude-3-7-sonnet"]
                : ["gpt-4.1-mini", "gpt-4.1"];

        public virtual Task<string> GetProviderAsync(ConversationScope scope, CancellationToken cancellationToken = default)
            => Task.FromResult("openai");

        public Task<string> SetProviderAsync(
            ConversationScope scope,
            string provider,
            CancellationToken cancellationToken = default)
            => Task.FromResult(provider);

        public virtual Task<string> GetModelAsync(
            ConversationScope scope,
            string provider,
            CancellationToken cancellationToken = default)
            => Task.FromResult(GetSupportedModels(provider).First());

        public Task<string> SetModelAsync(
            ConversationScope scope,
            string provider,
            string model,
            CancellationToken cancellationToken = default)
            => Task.FromResult(model);

        public virtual Task<bool> HasStoredApiKeyAsync(
            ConversationScope scope,
            string provider,
            CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task SetApiKeyAsync(
            ConversationScope scope,
            string provider,
            string apiKey,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RemoveApiKeyAsync(
            ConversationScope scope,
            string provider,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<AiRuntimeSettings> ResolveAsync(
            ConversationScope scope,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new AiRuntimeSettings("openai", "gpt-4.1-mini", string.Empty, AiApiKeySource.Missing));
    }

    private sealed class ThrowingAiRuntimeSettingsService : FakeAiRuntimeSettingsService
    {
        public override Task<string> GetProviderAsync(ConversationScope scope, CancellationToken cancellationToken = default)
            => Task.FromException<string>(new InvalidOperationException("AI provider unavailable."));

        public override Task<string> GetModelAsync(ConversationScope scope, string provider, CancellationToken cancellationToken = default)
            => Task.FromException<string>(new InvalidOperationException("AI model unavailable."));

        public override Task<bool> HasStoredApiKeyAsync(ConversationScope scope, string provider, CancellationToken cancellationToken = default)
            => Task.FromException<bool>(new InvalidOperationException("AI key unavailable."));
    }

    private class FakeNotionSyncProcessor : INotionSyncProcessor
    {
        public virtual Task<NotionSyncStatusSummary> GetStatusAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new NotionSyncStatusSummary(false, false, "disabled", 0, 0));

        public Task<NotionSyncRunSummary> ProcessPendingAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult(new NotionSyncRunSummary(take, 0, 0, 0, 0, 0));

        public Task<IReadOnlyList<NotionSyncFailedCard>> GetFailedCardsAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<NotionSyncFailedCard>>([]);

        public Task<int> RequeueFailedAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }

    private sealed class ThrowingNotionSyncProcessor : FakeNotionSyncProcessor
    {
        public override Task<NotionSyncStatusSummary> GetStatusAsync(CancellationToken cancellationToken = default)
            => Task.FromException<NotionSyncStatusSummary>(new InvalidOperationException("Notion unavailable."));
    }

    private class FakeFoodSyncService : IFoodSyncService
    {
        public Task<FoodSyncSummary> SyncFromNotionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new FoodSyncSummary(0, 0, 0, 0, false, null));

        public Task<int> SyncGroceryChangesToNotionAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<int> SyncInventoryChangesToNotionAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<int> ReconcileNotionGroceryOrphansAsync(TimeSpan? gracePeriod = null, CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public virtual Task<FoodSyncStatusSummary> GetSyncStatusAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new FoodSyncStatusSummary(0, 0, 0, 0));

        public Task<int> PurgeArchivedGroceryAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }

    private sealed class ThrowingFoodSyncService : FakeFoodSyncService
    {
        public override Task<FoodSyncStatusSummary> GetSyncStatusAsync(CancellationToken cancellationToken = default)
            => Task.FromException<FoodSyncStatusSummary>(new InvalidOperationException("Food sync unavailable."));
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
