namespace LagerthaAssistant.IntegrationTests.Controllers;

using LagerthaAssistant.Api.Contracts;
using LagerthaAssistant.Api.Controllers;
using LagerthaAssistant.Api.Services;
using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.Interfaces;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Localization;
using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.Application.Navigation;
using LagerthaAssistant.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using SharedBotKernel.Abstractions;
using SharedBotKernel.Infrastructure.AI;
using SharedBotKernel.Models.AI;
using SharedBotKernel.Models.Agents;
using Xunit;

public sealed class MiniAppSettingsControllerTests
{
    [Fact]
    public async Task Commit_ShouldRefreshTelegramMainKeyboardInSavedLocale()
    {
        var scopeAccessor = new FakeConversationScopeAccessor();
        var localeService = new FakeUserLocaleStateService { SetLocaleResult = "en" };
        var navigationState = new FakeNavigationStateService();
        var sender = new FakeTelegramBotSender();
        var commitService = CreateCommitService(localeService);
        var presenter = new TelegramNavigationPresenter(new LocalizationService(), "https://example.com/miniapp/settings");
        var sut = new MiniAppSettingsController(scopeAccessor, commitService, navigationState, presenter, sender);

        var response = await sut.Commit(
            new MiniAppSettingsCommitRequest(
                Locale: "en",
                SaveMode: "ask",
                StorageMode: "graph",
                AiProvider: "openai",
                AiModel: "gpt-4.1-mini",
                Channel: "telegram",
                UserId: "2002",
                ConversationId: "2002"),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<MiniAppSettingsCommitResponse>(ok.Value);

        Assert.Equal("en", payload.Locale);
        Assert.Equal("telegram", scopeAccessor.Current.Channel);
        Assert.Equal("2002", scopeAccessor.Current.UserId);
        Assert.Equal("2002", scopeAccessor.Current.ConversationId);
        Assert.Equal(NavigationSections.Main, navigationState.CurrentSection);
        Assert.Single(sender.SentMessages);

        var sent = sender.SentMessages.Single();
        Assert.Equal(2002L, sent.ChatId);
        Assert.Null(sent.MessageThreadId);

        var keyboard = Assert.IsType<TelegramReplyKeyboardMarkup>(sent.Options?.ReplyMarkup);
        var labels = keyboard.Keyboard.SelectMany(row => row).Select(button => button.Text).ToList();

        Assert.Contains(labels, x => x.Contains("Vocabulary", StringComparison.Ordinal));
        Assert.DoesNotContain(labels, x => x.Contains("Словник", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Commit_ShouldFallbackToUserId_WhenConversationIdIsDefault()
    {
        var scopeAccessor = new FakeConversationScopeAccessor();
        var localeService = new FakeUserLocaleStateService { SetLocaleResult = "uk" };
        var navigationState = new FakeNavigationStateService();
        var sender = new FakeTelegramBotSender();
        var commitService = CreateCommitService(localeService);
        var presenter = new TelegramNavigationPresenter(new LocalizationService(), "https://example.com/miniapp/settings");
        var sut = new MiniAppSettingsController(scopeAccessor, commitService, navigationState, presenter, sender);

        var response = await sut.Commit(
            new MiniAppSettingsCommitRequest(
                Locale: "uk",
                SaveMode: "ask",
                StorageMode: "graph",
                AiProvider: "openai",
                AiModel: "gpt-4.1-mini",
                Channel: "telegram",
                UserId: "2002",
                ConversationId: null),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<MiniAppSettingsCommitResponse>(ok.Value);

        Assert.Equal("uk", payload.Locale);
        Assert.Equal(ConversationScope.DefaultConversationId, scopeAccessor.Current.ConversationId);
        Assert.Equal(NavigationSections.Main, navigationState.CurrentSection);
        Assert.Single(sender.SentMessages);
        Assert.Equal(2002L, sender.SentMessages.Single().ChatId);
    }

    private static MiniAppSettingsCommitService CreateCommitService(FakeUserLocaleStateService localeService)
    {
        return new MiniAppSettingsCommitService(
            localeService,
            new FakeVocabularySaveModePreferenceService(),
            new FakeVocabularyStoragePreferenceService(),
            new FakeVocabularyStorageModeProvider(),
            new FakeAiRuntimeSettingsService());
    }

    private sealed class FakeConversationScopeAccessor : IConversationScopeAccessor
    {
        public ConversationScope Current { get; private set; } = ConversationScope.Default;

        public void Set(ConversationScope scope)
        {
            Current = scope;
        }
    }

    private sealed class FakeUserLocaleStateService : IUserLocaleStateService
    {
        public string? StoredLocale { get; set; }

        public string SetLocaleResult { get; set; } = "uk";

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
            bool selectedManually,
            CancellationToken cancellationToken = default)
        {
            StoredLocale = SetLocaleResult;
            return Task.FromResult(SetLocaleResult);
        }

        public Task<UserLocaleStateResult> EnsureLocaleAsync(
            string channel,
            string userId,
            string? telegramLanguageCode,
            string? incomingText,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new UserLocaleStateResult(SetLocaleResult, IsInitialized: false, IsSwitched: false));
        }
    }

    private sealed class FakeVocabularySaveModePreferenceService : IVocabularySaveModePreferenceService
    {
        public IReadOnlyList<string> SupportedModes => ["ask", "auto", "off"];

        public bool TryParse(string? value, out VocabularySaveMode mode)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case "ask":
                    mode = VocabularySaveMode.Ask;
                    return true;
                case "auto":
                    mode = VocabularySaveMode.Auto;
                    return true;
                case "off":
                    mode = VocabularySaveMode.Off;
                    return true;
                default:
                    mode = VocabularySaveMode.Ask;
                    return false;
            }
        }

        public string ToText(VocabularySaveMode mode) => mode switch
        {
            VocabularySaveMode.Auto => "auto",
            VocabularySaveMode.Off => "off",
            _ => "ask"
        };

        public Task<VocabularySaveMode> GetModeAsync(ConversationScope scope, CancellationToken cancellationToken = default)
            => Task.FromResult(VocabularySaveMode.Ask);

        public Task<VocabularySaveMode> SetModeAsync(
            ConversationScope scope,
            VocabularySaveMode mode,
            CancellationToken cancellationToken = default)
            => Task.FromResult(mode);
    }

    private sealed class FakeVocabularyStoragePreferenceService : IVocabularyStoragePreferenceService
    {
        public IReadOnlyList<string> SupportedModes => ["local", "graph"];

        public Task<VocabularyStorageMode> GetModeAsync(ConversationScope scope, CancellationToken cancellationToken = default)
            => Task.FromResult(VocabularyStorageMode.Graph);

        public Task<VocabularyStorageMode> SetModeAsync(
            ConversationScope scope,
            VocabularyStorageMode mode,
            CancellationToken cancellationToken = default)
            => Task.FromResult(mode);
    }

    private sealed class FakeVocabularyStorageModeProvider : IVocabularyStorageModeProvider
    {
        public VocabularyStorageMode CurrentMode { get; private set; } = VocabularyStorageMode.Graph;

        public void SetMode(VocabularyStorageMode mode)
        {
            CurrentMode = mode;
        }

        public bool TryParse(string? value, out VocabularyStorageMode mode)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case "local":
                    mode = VocabularyStorageMode.Local;
                    return true;
                case "graph":
                    mode = VocabularyStorageMode.Graph;
                    return true;
                default:
                    mode = VocabularyStorageMode.Graph;
                    return false;
            }
        }

        public string ToText(VocabularyStorageMode mode)
            => mode == VocabularyStorageMode.Local ? "local" : "graph";
    }

    private sealed class FakeAiRuntimeSettingsService : IAiRuntimeSettingsService
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

        public Task<string> GetProviderAsync(ConversationScope scope, CancellationToken cancellationToken = default)
            => Task.FromResult("openai");

        public Task<string> SetProviderAsync(
            ConversationScope scope,
            string provider,
            CancellationToken cancellationToken = default)
            => Task.FromResult(provider);

        public Task<string> GetModelAsync(
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

        public Task<bool> HasStoredApiKeyAsync(
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

    private sealed class FakeNavigationStateService : INavigationStateService
    {
        public string CurrentSection { get; private set; } = NavigationSections.Settings;

        public Task<string> GetCurrentSectionAsync(
            string channel,
            string userId,
            string conversationId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CurrentSection);
        }

        public Task<string> SetCurrentSectionAsync(
            string channel,
            string userId,
            string conversationId,
            string section,
            CancellationToken cancellationToken = default)
        {
            CurrentSection = section;
            return Task.FromResult(section);
        }
    }

    private sealed class FakeTelegramBotSender : ITelegramBotSender
    {
        public List<SentTelegramMessage> SentMessages { get; } = [];

        public Task<TelegramSendResult> SendTextAsync(
            long chatId,
            string text,
            TelegramSendOptions? options = null,
            int? messageThreadId = null,
            CancellationToken cancellationToken = default)
        {
            SentMessages.Add(new SentTelegramMessage(chatId, text, options, messageThreadId));
            return Task.FromResult(new TelegramSendResult(true));
        }

        public Task<TelegramSendResult> AnswerCallbackQueryAsync(
            string callbackQueryId,
            string? text = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TelegramSendResult(true));
        }
    }

    private sealed record SentTelegramMessage(
        long ChatId,
        string Text,
        TelegramSendOptions? Options,
        int? MessageThreadId);
}
