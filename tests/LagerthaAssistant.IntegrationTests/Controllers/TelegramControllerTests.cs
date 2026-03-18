namespace LagerthaAssistant.IntegrationTests.Controllers;

using LagerthaAssistant.Api.Contracts;
using LagerthaAssistant.Api.Controllers;
using LagerthaAssistant.Api.Interfaces;
using LagerthaAssistant.Api.Options;
using LagerthaAssistant.Application.Interfaces.AI;
using LagerthaAssistant.Application.Interfaces.Agents;
using LagerthaAssistant.Application.Interfaces;
using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Interfaces.Repositories;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.AI;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Models.Localization;
using LagerthaAssistant.Application.Navigation;
using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

public sealed class TelegramControllerTests
{
    [Fact]
    public async Task Webhook_ShouldMapScopeAndSendReply_ForTextMessage()
    {
        var orchestrator = new FakeConversationOrchestrator();
        var scopeAccessor = new FakeConversationScopeAccessor();
        var storageModeProvider = new FakeVocabularyStorageModeProvider();
        var storagePreferenceService = new FakeVocabularyStoragePreferenceService
        {
            CurrentMode = VocabularyStorageMode.Graph
        };
        var formatter = new FakeTelegramFormatter("assistant reply");
        var sender = new FakeTelegramBotSender();
        var navigationState = new FakeNavigationStateService { CurrentSection = "vocabulary" };

        var sut = CreateSut(
            orchestrator,
            scopeAccessor,
            storageModeProvider,
            storagePreferenceService,
            assistantSessionService: null,
            localeStateService: null,
            navigationStateService: navigationState,
            vocabularyCardRepository: null,
            navigationPresenter: null,
            formatter,
            sender,
            new TelegramOptions { Enabled = true });

        var update = BuildTextUpdate(chatId: 1001, userId: 2002, text: "void", messageThreadId: null);

        var response = await sut.Webhook(update, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);

        Assert.True(payload.Processed);
        Assert.True(payload.Replied);
        Assert.Equal("vocabulary.single", payload.Intent);
        Assert.Null(payload.Error);

        Assert.Equal("telegram", orchestrator.LastChannel);
        Assert.Equal("2002", orchestrator.LastUserId);
        Assert.Equal("1001", orchestrator.LastConversationId);
        Assert.Equal("void", orchestrator.LastInput);

        Assert.Equal(1001, sender.LastChatId);
        Assert.Equal("assistant reply", sender.LastText);
        Assert.Null(sender.LastMessageThreadId);

        Assert.Equal(VocabularyStorageMode.Graph, storageModeProvider.CurrentMode);
    }

    [Fact]
    public async Task Webhook_ShouldUseThreadBasedConversationId_WhenMessageThreadProvided()
    {
        var orchestrator = new FakeConversationOrchestrator();
        var navigationState = new FakeNavigationStateService { CurrentSection = "vocabulary" };
        var sut = CreateSut(
            orchestrator,
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            assistantSessionService: null,
            localeStateService: null,
            navigationStateService: navigationState,
            vocabularyCardRepository: null,
            navigationPresenter: null,
            new FakeTelegramFormatter("assistant reply"),
            new FakeTelegramBotSender(),
            new TelegramOptions { Enabled = true });

        var response = await sut.Webhook(
            BuildTextUpdate(chatId: -3001, userId: 2002, text: "prepare", messageThreadId: 88),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);

        Assert.True(payload.Processed);
        Assert.Equal("-3001:88", orchestrator.LastConversationId);
    }

    [Fact]
    public async Task Webhook_ShouldIgnoreUpdate_WhenNoTextMessageProvided()
    {
        var orchestrator = new FakeConversationOrchestrator();
        var sender = new FakeTelegramBotSender();
        var sut = CreateSut(
            orchestrator,
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            new FakeTelegramFormatter("assistant reply"),
            sender,
            new TelegramOptions { Enabled = true });

        var update = new TelegramWebhookUpdateRequest(
            UpdateId: 1,
            Message: new TelegramIncomingMessage(
                MessageId: 5,
                From: new TelegramUserInfo(2002, false, "en", "mike", "Mike", null),
                Chat: new TelegramChatInfo(1001, "private", "mike", null),
                Text: null,
                Caption: null,
                MessageThreadId: null),
            EditedMessage: null,
            CallbackQuery: null);

        var response = await sut.Webhook(update, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);

        Assert.False(payload.Processed);
        Assert.False(payload.Replied);
        Assert.Equal(0, orchestrator.Calls);
        Assert.Equal(0, sender.Calls);
    }

    [Fact]
    public async Task Webhook_ShouldReturnUnauthorized_WhenWebhookSecretIsInvalid()
    {
        var sut = CreateSut(
            new FakeConversationOrchestrator(),
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            new FakeTelegramFormatter("assistant reply"),
            new FakeTelegramBotSender(),
            new TelegramOptions
            {
                Enabled = true,
                WebhookSecret = "expected-secret"
            });

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Telegram-Bot-Api-Secret-Token"] = "wrong-secret";
        sut.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        var response = await sut.Webhook(BuildTextUpdate(1001, 2002, "void", null), CancellationToken.None);
        Assert.IsType<UnauthorizedResult>(response.Result);
    }

    [Fact]
    public async Task Webhook_ShouldHandleStartCommand_AndSendMainReplyKeyboard()
    {
        var sender = new FakeTelegramBotSender();
        var sut = CreateSut(
            new FakeConversationOrchestrator(),
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            new FakeTelegramFormatter("ignored"),
            sender,
            new TelegramOptions { Enabled = true });

        var response = await sut.Webhook(BuildTextUpdate(1001, 2002, "/start", null), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);

        Assert.True(payload.Replied);
        Assert.Equal("nav.start", payload.Intent);
        Assert.IsType<TelegramReplyKeyboardMarkup>(sender.LastOptions?.ReplyMarkup);
        Assert.Null(sender.LastOptions?.ParseMode);
    }

    [Fact]
    public async Task Webhook_ShouldHandleNavMainCallback_AndSendMainReplyKeyboard()
    {
        var sender = new FakeTelegramBotSender();
        var sut = CreateSut(
            new FakeConversationOrchestrator(),
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            new FakeTelegramFormatter("ignored"),
            sender,
            new TelegramOptions { Enabled = true });

        var response = await sut.Webhook(BuildCallbackUpdate(1001, 2002, "nav:main", null), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);

        Assert.True(payload.Replied);
        Assert.Equal("nav.main", payload.Intent);
        Assert.IsType<TelegramReplyKeyboardMarkup>(sender.LastOptions?.ReplyMarkup);
        Assert.Null(sender.LastOptions?.ParseMode);
        Assert.Equal(1, sender.CallbackAnswers);
        Assert.Equal("cb-1", sender.LastCallbackQueryId);
    }

    [Fact]
    public async Task Webhook_ShouldHandleVocabBatchCallback_WithoutUnsupportedCommandMessage()
    {
        var orchestrator = new FakeConversationOrchestrator();
        var sender = new FakeTelegramBotSender();
        var sut = CreateSut(
            orchestrator,
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            new FakeTelegramFormatter("ignored"),
            sender,
            new TelegramOptions { Enabled = true });

        var response = await sut.Webhook(BuildCallbackUpdate(1001, 2002, "vocab:batch", null), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);

        Assert.True(payload.Replied);
        Assert.Equal("vocab.batch", payload.Intent);
        Assert.DoesNotContain("Unsupported command in API mode", sender.LastText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, orchestrator.Calls);
    }

    private static TelegramController CreateSut(
        FakeConversationOrchestrator orchestrator,
        FakeConversationScopeAccessor scopeAccessor,
        FakeVocabularyStorageModeProvider storageModeProvider,
        FakeVocabularyStoragePreferenceService storagePreferenceService,
        FakeTelegramFormatter formatter,
        FakeTelegramBotSender sender,
        TelegramOptions options,
        FakeTelegramProcessedUpdateRepository? processedUpdates = null)
    {
        return CreateSut(
            orchestrator,
            scopeAccessor,
            storageModeProvider,
            storagePreferenceService,
            assistantSessionService: null,
            formatter,
            sender,
            options,
            processedUpdates);
    }

    private static TelegramController CreateSut(
        FakeConversationOrchestrator orchestrator,
        FakeConversationScopeAccessor scopeAccessor,
        FakeVocabularyStorageModeProvider storageModeProvider,
        FakeVocabularyStoragePreferenceService storagePreferenceService,
        FakeAssistantSessionService? assistantSessionService,
        FakeTelegramFormatter formatter,
        FakeTelegramBotSender sender,
        TelegramOptions options,
        FakeTelegramProcessedUpdateRepository? processedUpdates = null)
    {
        return CreateSut(
            orchestrator,
            scopeAccessor,
            storageModeProvider,
            storagePreferenceService,
            assistantSessionService,
            localeStateService: null,
            navigationStateService: null,
            vocabularyCardRepository: null,
            navigationPresenter: null,
            formatter,
            sender,
            options,
            processedUpdates);
    }

    private static TelegramController CreateSut(
        FakeConversationOrchestrator orchestrator,
        FakeConversationScopeAccessor scopeAccessor,
        FakeVocabularyStorageModeProvider storageModeProvider,
        FakeVocabularyStoragePreferenceService storagePreferenceService,
        FakeAssistantSessionService? assistantSessionService,
        FakeUserLocaleStateService? localeStateService,
        FakeNavigationStateService? navigationStateService,
        FakeVocabularyCardRepository? vocabularyCardRepository,
        FakeTelegramNavigationPresenter? navigationPresenter,
        FakeTelegramFormatter formatter,
        FakeTelegramBotSender sender,
        TelegramOptions options,
        FakeTelegramProcessedUpdateRepository? processedUpdates = null)
    {
        return new TelegramController(
            orchestrator,
            assistantSessionService ?? new FakeAssistantSessionService(),
            scopeAccessor,
            storageModeProvider,
            storagePreferenceService,
            localeStateService ?? new FakeUserLocaleStateService(),
            navigationStateService ?? new FakeNavigationStateService(),
            new NavigationRouter(),
            vocabularyCardRepository ?? new FakeVocabularyCardRepository(),
            navigationPresenter ?? new FakeTelegramNavigationPresenter(),
            formatter,
            sender,
            processedUpdates ?? new FakeTelegramProcessedUpdateRepository(),
            Options.Create(options),
            NullLogger<TelegramController>.Instance);
    }

    private static TelegramWebhookUpdateRequest BuildTextUpdate(long chatId, long userId, string text, int? messageThreadId)
    {
        return new TelegramWebhookUpdateRequest(
            UpdateId: 1,
            Message: new TelegramIncomingMessage(
                MessageId: 10,
                From: new TelegramUserInfo(userId, false, "en", "mike", "Mike", null),
                Chat: new TelegramChatInfo(chatId, "private", "mike", null),
                Text: text,
                Caption: null,
                MessageThreadId: messageThreadId),
            EditedMessage: null,
            CallbackQuery: null);
    }

    private static TelegramWebhookUpdateRequest BuildCallbackUpdate(long chatId, long userId, string callbackData, int? messageThreadId)
    {
        return new TelegramWebhookUpdateRequest(
            UpdateId: 2,
            Message: null,
            EditedMessage: null,
            CallbackQuery: new TelegramCallbackQuery(
                Id: "cb-1",
                From: new TelegramUserInfo(userId, false, "en", "mike", "Mike", null),
                Message: new TelegramIncomingMessage(
                    MessageId: 99,
                    From: new TelegramUserInfo(userId, false, "en", "mike", "Mike", null),
                    Chat: new TelegramChatInfo(chatId, "private", "mike", null),
                    Text: null,
                    Caption: null,
                    MessageThreadId: messageThreadId),
                Data: callbackData));
    }

    private sealed class FakeConversationOrchestrator : IConversationOrchestrator
    {
        public int Calls { get; private set; }

        public string LastInput { get; private set; } = string.Empty;

        public string LastChannel { get; private set; } = string.Empty;

        public string? LastUserId { get; private set; }

        public string? LastConversationId { get; private set; }

        public ConversationAgentResult NextResult { get; set; } = new(
            AgentName: "vocabulary-agent",
            Intent: "vocabulary.single",
            IsBatch: false,
            Items: []);

        public Task<ConversationAgentResult> ProcessAsync(string input, CancellationToken cancellationToken = default)
            => ProcessAsync(input, "unknown", null, null, cancellationToken);

        public Task<ConversationAgentResult> ProcessAsync(
            string input,
            string channel,
            CancellationToken cancellationToken = default)
            => ProcessAsync(input, channel, null, null, cancellationToken);

        public Task<ConversationAgentResult> ProcessAsync(
            string input,
            string channel,
            string? userId,
            string? conversationId,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            LastInput = input;
            LastChannel = channel;
            LastUserId = userId;
            LastConversationId = conversationId;
            return Task.FromResult(NextResult);
        }
    }

    private sealed class FakeConversationScopeAccessor : IConversationScopeAccessor
    {
        public ConversationScope Current { get; private set; } = ConversationScope.Default;

        public void Set(ConversationScope scope)
        {
            Current = scope;
        }
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
            if (string.Equals(value, "local", StringComparison.OrdinalIgnoreCase))
            {
                mode = VocabularyStorageMode.Local;
                return true;
            }

            if (string.Equals(value, "graph", StringComparison.OrdinalIgnoreCase))
            {
                mode = VocabularyStorageMode.Graph;
                return true;
            }

            mode = VocabularyStorageMode.Local;
            return false;
        }

        public string ToText(VocabularyStorageMode mode)
            => mode.ToString().ToLowerInvariant();
    }

    private sealed class FakeVocabularyStoragePreferenceService : IVocabularyStoragePreferenceService
    {
        public IReadOnlyList<string> SupportedModes { get; set; } = ["local", "graph"];

        public VocabularyStorageMode CurrentMode { get; set; } = VocabularyStorageMode.Local;

        public Task<VocabularyStorageMode> GetModeAsync(ConversationScope scope, CancellationToken cancellationToken = default)
            => Task.FromResult(CurrentMode);

        public Task<VocabularyStorageMode> SetModeAsync(
            ConversationScope scope,
            VocabularyStorageMode mode,
            CancellationToken cancellationToken = default)
        {
            CurrentMode = mode;
            return Task.FromResult(mode);
        }
    }

    private sealed class FakeAssistantSessionService : IAssistantSessionService
    {
        public IReadOnlyCollection<LagerthaAssistant.Domain.AI.ConversationMessage> Messages => [];

        public string NextContent { get; set; } = "assistant reply";

        public Task<AssistantCompletionResult> AskAsync(string userMessage, CancellationToken cancellationToken = default)
            => Task.FromResult(new AssistantCompletionResult(NextContent, "test-model", null));

        public Task<IReadOnlyCollection<LagerthaAssistant.Domain.AI.ConversationMessage>> GetRecentHistoryAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<LagerthaAssistant.Domain.AI.ConversationMessage>>([]);

        public Task<IReadOnlyCollection<UserMemoryEntry>> GetActiveMemoryAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<UserMemoryEntry>>([]);

        public Task<string> GetSystemPromptAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(string.Empty);

        public Task<IReadOnlyCollection<SystemPromptEntry>> GetSystemPromptHistoryAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<SystemPromptEntry>>([]);

        public Task<IReadOnlyCollection<SystemPromptProposal>> GetSystemPromptProposalsAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<SystemPromptProposal>>([]);

        public Task<SystemPromptProposal> CreateSystemPromptProposalAsync(string prompt, string reason, double confidence, string source = "manual", CancellationToken cancellationToken = default)
            => Task.FromResult(new SystemPromptProposal());

        public Task<SystemPromptProposal> GenerateSystemPromptProposalAsync(string goal, CancellationToken cancellationToken = default)
            => Task.FromResult(new SystemPromptProposal());

        public Task<string> ApplySystemPromptProposalAsync(int proposalId, CancellationToken cancellationToken = default)
            => Task.FromResult(string.Empty);

        public Task RejectSystemPromptProposalAsync(int proposalId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<string> SetSystemPromptAsync(string prompt, string source = "manual", CancellationToken cancellationToken = default)
            => Task.FromResult(prompt);

        public void Reset()
        {
        }
    }

    private sealed class FakeUserLocaleStateService : IUserLocaleStateService
    {
        public string NextLocale { get; set; } = "en";

        public Task<UserLocaleStateResult> EnsureLocaleAsync(
            string channel,
            string userId,
            string? telegramLanguageCode,
            string? incomingText,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new UserLocaleStateResult(NextLocale, IsInitialized: false, IsSwitched: false));
        }
    }

    private sealed class FakeNavigationStateService : INavigationStateService
    {
        public string CurrentSection { get; set; } = "main";

        public Task<string> GetCurrentSectionAsync(
            string channel,
            string userId,
            string conversationId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(CurrentSection);

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

    private sealed class FakeVocabularyCardRepository : IVocabularyCardRepository
    {
        public Task<IReadOnlyList<VocabularyCard>> FindByAnyTokenAsync(IReadOnlyCollection<string> normalizedTokens, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<VocabularyCard>>([]);

        public Task<VocabularyCard?> GetByIdentityAsync(string normalizedWord, string deckFileName, string storageMode, CancellationToken cancellationToken = default)
            => Task.FromResult<VocabularyCard?>(null);

        public Task AddAsync(VocabularyCard card, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<int> CountPendingNotionSyncAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<int> CountFailedNotionSyncAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<IReadOnlyList<VocabularyCard>> ClaimPendingNotionSyncAsync(int take, DateTimeOffset claimedAtUtc, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<VocabularyCard>>([]);

        public Task<IReadOnlyList<VocabularyCard>> GetFailedNotionSyncAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<VocabularyCard>>([]);

        public Task<int> RequeueFailedNotionSyncAsync(int take, DateTimeOffset requeuedAtUtc, CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<int> CountAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<IReadOnlyList<VocabularyCard>> GetRecentAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<VocabularyCard>>([]);

        public Task<int> DeleteAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }

    private sealed class FakeTelegramNavigationPresenter : ITelegramNavigationPresenter
    {
        public MainMenuLabels GetMainMenuLabels(string locale)
            => new("🗣 Chat", "📚 Vocabulary", "🛒 Shopping", "🍽 Menu");

        public string GetText(string key, string locale, params object[] args)
        {
            var value = key switch
            {
                "menu.main.title" => "What can I help you with?",
                "start.welcome" => "Welcome",
                "stub.wip" => "WIP",
                "menu.vocabulary.title" => "Vocabulary {0}",
                "vocab.add.prompt" => "Add word",
                "vocab.url.prompt" => "Send URL",
                "vocab.list.empty" => "Empty",
                "vocab.list.title" => "Latest words",
                _ => key
            };

            return args.Length == 0
                ? value
                : string.Format(value, args);
        }

        public TelegramReplyKeyboardMarkup BuildMainReplyKeyboard(string locale)
            => new([[new TelegramKeyboardButton("🗣 Chat"), new TelegramKeyboardButton("📚 Vocabulary")], [new TelegramKeyboardButton("🛒 Shopping"), new TelegramKeyboardButton("🍽 Menu")]]);

        public TelegramInlineKeyboardMarkup BuildVocabularyKeyboard(string locale)
            => new([[new TelegramInlineKeyboardButton("Add", "vocab:add")]]);

        public TelegramInlineKeyboardMarkup BuildShoppingKeyboard(string locale)
            => new([[new TelegramInlineKeyboardButton("Add", "shop:add")]]);

        public TelegramInlineKeyboardMarkup BuildWeeklyMenuKeyboard(string locale)
            => new([[new TelegramInlineKeyboardButton("View", "weekly:view")]]);
    }

    private sealed class FakeTelegramFormatter : ITelegramConversationResponseFormatter
    {
        private readonly string _formatted;

        public FakeTelegramFormatter(string formatted)
        {
            _formatted = formatted;
        }

        public string Format(ConversationAgentResult result) => _formatted;
    }

    private sealed class FakeTelegramBotSender : ITelegramBotSender
    {
        public int Calls { get; private set; }
        public int CallbackAnswers { get; private set; }

        public long LastChatId { get; private set; }

        public string LastText { get; private set; } = string.Empty;
        public string? LastCallbackQueryId { get; private set; }

        public TelegramSendOptions? LastOptions { get; private set; }

        public int? LastMessageThreadId { get; private set; }

        public bool SimulateFailure { get; set; }
        public bool SimulateCallbackAnswerFailure { get; set; }

        public Task<TelegramSendResult> SendTextAsync(
            long chatId,
            string text,
            TelegramSendOptions? options = null,
            int? messageThreadId = null,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            LastChatId = chatId;
            LastText = text;
            LastOptions = options;
            LastMessageThreadId = messageThreadId;
            var result = SimulateFailure
                ? new TelegramSendResult(false, "Simulated send failure")
                : new TelegramSendResult(true);
            return Task.FromResult(result);
        }

        public Task<TelegramSendResult> AnswerCallbackQueryAsync(
            string callbackQueryId,
            string? text = null,
            CancellationToken cancellationToken = default)
        {
            CallbackAnswers++;
            LastCallbackQueryId = callbackQueryId;
            var result = SimulateCallbackAnswerFailure
                ? new TelegramSendResult(false, "Simulated callback answer failure")
                : new TelegramSendResult(true);
            return Task.FromResult(result);
        }
    }

    private sealed class FakeTelegramProcessedUpdateRepository : ITelegramProcessedUpdateRepository
    {
        private readonly HashSet<long> _processed = [];

        public int MarkCalls { get; private set; }

        public void Seed(long updateId) => _processed.Add(updateId);

        public Task<bool> IsProcessedAsync(long updateId, CancellationToken cancellationToken = default)
            => Task.FromResult(_processed.Contains(updateId));

        public Task MarkProcessedAsync(long updateId, CancellationToken cancellationToken = default)
        {
            MarkCalls++;
            _processed.Add(updateId);
            return Task.CompletedTask;
        }

        public Task DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    [Fact]
    public async Task Webhook_ShouldReturnProcessedWithoutReply_WhenUpdateAlreadyProcessed()
    {
        var orchestrator = new FakeConversationOrchestrator();
        var processedUpdates = new FakeTelegramProcessedUpdateRepository();
        processedUpdates.Seed(updateId: 1);

        var sut = CreateSut(
            orchestrator,
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            new FakeTelegramFormatter("reply"),
            new FakeTelegramBotSender(),
            new TelegramOptions { Enabled = true },
            processedUpdates);

        var update = BuildTextUpdate(chatId: 1001, userId: 2002, text: "void", messageThreadId: null);

        var response = await sut.Webhook(update, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);

        Assert.True(payload.Processed);
        Assert.False(payload.Replied);
        Assert.Equal(0, orchestrator.Calls);
    }

    [Fact]
    public async Task Webhook_ShouldReturn200AndMarkProcessed_WhenSendFails()
    {
        var processedUpdates = new FakeTelegramProcessedUpdateRepository();
        var sender = new FakeTelegramBotSender { SimulateFailure = true };

        var sut = CreateSut(
            new FakeConversationOrchestrator(),
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            new FakeTelegramFormatter("reply"),
            sender,
            new TelegramOptions { Enabled = true },
            processedUpdates);

        var update = BuildTextUpdate(chatId: 1001, userId: 2002, text: "void", messageThreadId: null);

        var response = await sut.Webhook(update, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);
        Assert.True(payload.Processed);
        Assert.False(payload.Replied);
        Assert.NotNull(payload.Error);
        Assert.Equal(1, processedUpdates.MarkCalls);
    }

    [Fact]
    public async Task Webhook_ShouldProcessAndReply_WhenOnlyEditedMessagePresent()
    {
        var orchestrator = new FakeConversationOrchestrator();
        var sender = new FakeTelegramBotSender();
        var navigationState = new FakeNavigationStateService { CurrentSection = "vocabulary" };

        var sut = CreateSut(
            orchestrator,
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            assistantSessionService: null,
            localeStateService: null,
            navigationStateService: navigationState,
            vocabularyCardRepository: null,
            navigationPresenter: null,
            new FakeTelegramFormatter("edited reply"),
            sender,
            new TelegramOptions { Enabled = true });

        var editedMessage = new TelegramIncomingMessage(
            MessageId: 20,
            From: new TelegramUserInfo(2002, false, "en", "mike", "Mike", null),
            Chat: new TelegramChatInfo(1001, "private", "mike", null),
            Text: "corrected",
            Caption: null,
            MessageThreadId: null);

        var update = new TelegramWebhookUpdateRequest(
            UpdateId: 99,
            Message: null,
            EditedMessage: editedMessage,
            CallbackQuery: null);

        var response = await sut.Webhook(update, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);

        Assert.True(payload.Processed);
        Assert.True(payload.Replied);
        Assert.Equal("corrected", orchestrator.LastInput);
        Assert.Equal(1001, sender.LastChatId);
    }

    [Fact]
    public async Task Webhook_ShouldPassWithNoSecret_WhenSecretNotConfigured()
    {
        var sut = CreateSut(
            new FakeConversationOrchestrator(),
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            new FakeTelegramFormatter("reply"),
            new FakeTelegramBotSender(),
            new TelegramOptions { Enabled = true, WebhookSecret = null });

        // No secret header set — should still pass when no secret is configured
        var response = await sut.Webhook(BuildTextUpdate(1001, 2002, "void", null), CancellationToken.None);
        Assert.IsType<OkObjectResult>(response.Result);
    }

    [Fact]
    public async Task Webhook_ShouldReturnUnauthorized_WhenSecretHeaderMissing()
    {
        var sut = CreateSut(
            new FakeConversationOrchestrator(),
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            new FakeTelegramFormatter("reply"),
            new FakeTelegramBotSender(),
            new TelegramOptions { Enabled = true, WebhookSecret = "required-secret" });

        // Set up HttpContext without the secret header
        sut.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var response = await sut.Webhook(BuildTextUpdate(1001, 2002, "void", null), CancellationToken.None);
        Assert.IsType<UnauthorizedResult>(response.Result);
    }

    [Fact]
    public async Task Webhook_ShouldReturnOk_WhenCallbackAnswerFails()
    {
        var sender = new FakeTelegramBotSender { SimulateCallbackAnswerFailure = true };
        var sut = CreateSut(
            new FakeConversationOrchestrator(),
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            new FakeTelegramFormatter("reply"),
            sender,
            new TelegramOptions { Enabled = true });

        var response = await sut.Webhook(BuildCallbackUpdate(1001, 2002, "vocab:add", null), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);
        Assert.True(payload.Processed);
        Assert.Equal(1, sender.CallbackAnswers);
    }
}
