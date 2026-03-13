namespace LagerthaAssistant.IntegrationTests.Controllers;

using LagerthaAssistant.Api.Contracts;
using LagerthaAssistant.Api.Controllers;
using LagerthaAssistant.Api.Interfaces;
using LagerthaAssistant.Api.Options;
using LagerthaAssistant.Application.Interfaces.Agents;
using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Models.Vocabulary;
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

        var sut = CreateSut(
            orchestrator,
            scopeAccessor,
            storageModeProvider,
            storagePreferenceService,
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
        var sut = CreateSut(
            orchestrator,
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
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
                From: new TelegramUserInfo(2002, false, "mike", "Mike", null),
                Chat: new TelegramChatInfo(1001, "private", "mike", null),
                Text: null,
                Caption: null,
                MessageThreadId: null),
            EditedMessage: null);

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

    private static TelegramController CreateSut(
        FakeConversationOrchestrator orchestrator,
        FakeConversationScopeAccessor scopeAccessor,
        FakeVocabularyStorageModeProvider storageModeProvider,
        FakeVocabularyStoragePreferenceService storagePreferenceService,
        FakeTelegramFormatter formatter,
        FakeTelegramBotSender sender,
        TelegramOptions options)
    {
        return new TelegramController(
            orchestrator,
            scopeAccessor,
            storageModeProvider,
            storagePreferenceService,
            formatter,
            sender,
            Options.Create(options),
            NullLogger<TelegramController>.Instance);
    }

    private static TelegramWebhookUpdateRequest BuildTextUpdate(long chatId, long userId, string text, int? messageThreadId)
    {
        return new TelegramWebhookUpdateRequest(
            UpdateId: 1,
            Message: new TelegramIncomingMessage(
                MessageId: 10,
                From: new TelegramUserInfo(userId, false, "mike", "Mike", null),
                Chat: new TelegramChatInfo(chatId, "private", "mike", null),
                Text: text,
                Caption: null,
                MessageThreadId: messageThreadId),
            EditedMessage: null);
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

        public long LastChatId { get; private set; }

        public string LastText { get; private set; } = string.Empty;

        public int? LastMessageThreadId { get; private set; }

        public Task<TelegramSendResult> SendTextAsync(
            long chatId,
            string text,
            int? messageThreadId = null,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            LastChatId = chatId;
            LastText = text;
            LastMessageThreadId = messageThreadId;
            return Task.FromResult(new TelegramSendResult(true));
        }
    }
}
