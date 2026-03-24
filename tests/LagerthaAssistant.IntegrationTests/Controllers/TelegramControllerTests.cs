namespace LagerthaAssistant.IntegrationTests.Controllers;

using LagerthaAssistant.Api.Contracts;
using LagerthaAssistant.Api.Controllers;
using LagerthaAssistant.Api.Interfaces;
using LagerthaAssistant.Api.Options;
using LagerthaAssistant.Api.Services;
using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.Interfaces.AI;
using LagerthaAssistant.Application.Interfaces.Agents;
using LagerthaAssistant.Application.Interfaces;
using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Interfaces.Food;
using LagerthaAssistant.Application.Interfaces.Repositories;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Food;
using LagerthaAssistant.Application.Models.AI;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Models.Localization;
using LagerthaAssistant.Application.Navigation;
using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.Application.Services.Vocabulary;
using LagerthaAssistant.Domain.Entities;
using LagerthaAssistant.Infrastructure.Options;
using LagerthaAssistant.Infrastructure.Services;
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
    public async Task Webhook_ShouldForceGraphStorageMode_ForTelegramEvenWhenPreferenceIsLocal()
    {
        var orchestrator = new FakeConversationOrchestrator();
        var scopeAccessor = new FakeConversationScopeAccessor();
        var storageModeProvider = new FakeVocabularyStorageModeProvider();
        var storagePreferenceService = new FakeVocabularyStoragePreferenceService
        {
            CurrentMode = VocabularyStorageMode.Local
        };

        var sut = CreateSut(
            orchestrator,
            scopeAccessor,
            storageModeProvider,
            storagePreferenceService,
            assistantSessionService: null,
            localeStateService: null,
            navigationStateService: new FakeNavigationStateService { CurrentSection = NavigationSections.Vocabulary },
            vocabularyCardRepository: null,
            navigationPresenter: null,
            new FakeTelegramFormatter("assistant reply"),
            new FakeTelegramBotSender(),
            new TelegramOptions { Enabled = true });

        var response = await sut.Webhook(BuildTextUpdate(chatId: 1001, userId: 2002, text: "void", messageThreadId: null), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);
        Assert.True(payload.Processed);
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
        Assert.Equal("HTML", sender.LastOptions?.ParseMode);
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
        Assert.Equal("HTML", sender.LastOptions?.ParseMode);
        Assert.Equal(1, sender.CallbackAnswers);
        Assert.Equal("cb-1", sender.LastCallbackQueryId);
    }

    [Fact]
    public async Task Webhook_ShouldRouteFreeTextToAssistantFlow_AfterChatButtonPressed()
    {
        var orchestrator = new FakeConversationOrchestrator
        {
            NextResult = ConversationAgentResult.Empty(
                agentName: "assistant-agent",
                intent: "assistant.chat",
                message: "assistant reply")
        };
        var sender = new FakeTelegramBotSender();
        var navigationState = new FakeNavigationStateService { CurrentSection = NavigationSections.Settings };
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
            new FakeTelegramFormatter("formatted"),
            sender,
            new TelegramOptions { Enabled = true });

        _ = await sut.Webhook(BuildTextUpdate(1001, 2002, "Chat", null, updateId: 91), CancellationToken.None);
        var response = await sut.Webhook(BuildTextUpdate(1001, 2002, "What can you do?", null, updateId: 92), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);

        Assert.True(payload.Replied);
        Assert.Equal("assistant.chat", payload.Intent);
        Assert.StartsWith(ConversationInputMarkers.Chat, orchestrator.LastInput, StringComparison.Ordinal);
        Assert.Equal(NavigationSections.Chat, navigationState.CurrentSection);
    }

    [Fact]
    public async Task Webhook_ShouldRefreshMainKeyboardLocaleInChat_WhenAssistantUpdatesLanguage()
    {
        var localeState = new FakeUserLocaleStateService
        {
            StoredLocale = LocalizationConstants.UkrainianLocale,
            NextLocale = LocalizationConstants.UkrainianLocale
        };
        var orchestrator = new FakeConversationOrchestrator
        {
            NextResult = ConversationAgentResult.Empty(
                agentName: "assistant-agent",
                intent: "assistant.settings.language.updated",
                message: "Language changed")
        };
        orchestrator.OnProcess = (_, _, _, _) =>
        {
            localeState.StoredLocale = LocalizationConstants.EnglishLocale;
        };

        var sender = new FakeTelegramBotSender();
        var navigationState = new FakeNavigationStateService { CurrentSection = NavigationSections.Main };
        var presenter = new FakeTelegramNavigationPresenter();
        var sut = CreateSut(
            orchestrator,
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            assistantSessionService: null,
            localeStateService: localeState,
            navigationStateService: navigationState,
            vocabularyCardRepository: null,
            navigationPresenter: presenter,
            new FakeTelegramFormatter("formatted"),
            sender,
            new TelegramOptions { Enabled = true });

        _ = await sut.Webhook(BuildTextUpdate(1001, 2002, "Chat", null, updateId: 901), CancellationToken.None);
        var response = await sut.Webhook(
            BuildTextUpdate(1001, 2002, "Зміни мову на англійську", null, updateId: 902),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);

        Assert.True(payload.Replied);
        Assert.Equal("assistant.settings.language.updated", payload.Intent);
        Assert.Equal(LocalizationConstants.EnglishLocale, presenter.LastMainReplyKeyboardLocale);
    }

    [Fact]
    public async Task Webhook_ShouldHandleAddWordFlowInsideChat_WhenAssistantStartsAddWordFlow()
    {
        var orchestrator = new FakeConversationOrchestrator
        {
            NextResult = ConversationAgentResult.Empty(
                agentName: "assistant-agent",
                intent: "assistant.vocabulary.add.start",
                message: "Please send the word you want to add.")
        };

        var sender = new FakeTelegramBotSender();
        var navigationState = new FakeNavigationStateService { CurrentSection = NavigationSections.Settings };
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
            new FakeTelegramFormatter("formatted"),
            sender,
            new TelegramOptions { Enabled = true });

        _ = await sut.Webhook(BuildTextUpdate(1001, 2002, "Chat", null, updateId: 191), CancellationToken.None);

        var startAddFlow = await sut.Webhook(
            BuildTextUpdate(1001, 2002, "Додай слово у словник", null, updateId: 192),
            CancellationToken.None);

        var startOk = Assert.IsType<OkObjectResult>(startAddFlow.Result);
        var startPayload = Assert.IsType<TelegramWebhookResponse>(startOk.Value);
        Assert.True(startPayload.Replied);
        Assert.Equal("vocab.add", startPayload.Intent);
        Assert.Equal(NavigationSections.Chat, navigationState.CurrentSection);
        Assert.StartsWith(ConversationInputMarkers.Chat, orchestrator.LastInput, StringComparison.Ordinal);

        orchestrator.NextResult = BuildVocabularySingleResult();

        var lookupResponse = await sut.Webhook(
            BuildTextUpdate(1001, 2002, "root", null, updateId: 193),
            CancellationToken.None);

        var lookupOk = Assert.IsType<OkObjectResult>(lookupResponse.Result);
        var lookupPayload = Assert.IsType<TelegramWebhookResponse>(lookupOk.Value);
        Assert.True(lookupPayload.Replied);
        Assert.Equal("vocabulary.single", lookupPayload.Intent);
        Assert.Equal("root", orchestrator.LastInput);
        Assert.Equal(NavigationSections.Chat, navigationState.CurrentSection);
    }

    [Fact]
    public async Task Webhook_ShouldProcessInlineAddWordRequestInChat_WhenWordIsProvided()
    {
        var orchestrator = new FakeConversationOrchestrator();
        orchestrator.NextResults.Enqueue(ConversationAgentResult.Empty(
            agentName: "assistant-agent",
            intent: "assistant.vocabulary.add.start",
            message: "Please send the word you want to add."));
        orchestrator.NextResults.Enqueue(BuildVocabularySingleResult("goal"));

        var sender = new FakeTelegramBotSender();
        var navigationState = new FakeNavigationStateService { CurrentSection = NavigationSections.Main };
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
            new FakeTelegramFormatter("formatted"),
            sender,
            new TelegramOptions { Enabled = true });

        _ = await sut.Webhook(BuildTextUpdate(1001, 2002, "Chat", null, updateId: 221), CancellationToken.None);
        var response = await sut.Webhook(BuildTextUpdate(1001, 2002, "Додай слово goal", null, updateId: 222), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);

        Assert.True(payload.Replied);
        Assert.Equal("vocabulary.single", payload.Intent);
        Assert.Equal("goal", orchestrator.LastInput);
        Assert.Equal(NavigationSections.Chat, navigationState.CurrentSection);
    }

    [Fact]
    public async Task Webhook_ShouldExecuteSettingsActionInChat_AndStayInChatSection()
    {
        var orchestrator = new FakeConversationOrchestrator
        {
            NextResult = ConversationAgentResult.Empty(
                agentName: "assistant-agent",
                intent: "assistant.settings.open",
                message: "Opening settings.")
        };

        var sender = new FakeTelegramBotSender();
        var navigationState = new FakeNavigationStateService { CurrentSection = NavigationSections.Main };
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
            new FakeTelegramFormatter("formatted"),
            sender,
            new TelegramOptions { Enabled = true });

        _ = await sut.Webhook(BuildTextUpdate(1001, 2002, "Chat", null, updateId: 201), CancellationToken.None);
        var response = await sut.Webhook(BuildTextUpdate(1001, 2002, "Відкрий налаштування", null, updateId: 202), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);

        Assert.True(payload.Replied);
        Assert.Equal("settings.section", payload.Intent);
        Assert.Equal(NavigationSections.Chat, navigationState.CurrentSection);
        Assert.Contains("Settings", sender.LastText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Webhook_ShouldExecuteLanguagePanelActionInChat_AndStayInChatSection()
    {
        var orchestrator = new FakeConversationOrchestrator
        {
            NextResult = ConversationAgentResult.Empty(
                agentName: "assistant-agent",
                intent: "assistant.settings.language.open",
                message: "Opening language settings.")
        };

        var sender = new FakeTelegramBotSender();
        var navigationState = new FakeNavigationStateService { CurrentSection = NavigationSections.Main };
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
            new FakeTelegramFormatter("formatted"),
            sender,
            new TelegramOptions { Enabled = true });

        _ = await sut.Webhook(BuildTextUpdate(1001, 2002, "Chat", null, updateId: 205), CancellationToken.None);
        var response = await sut.Webhook(BuildTextUpdate(1001, 2002, "Покажи мови", null, updateId: 206), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);

        Assert.True(payload.Replied);
        Assert.Equal("settings.language", payload.Intent);
        Assert.Equal(NavigationSections.Chat, navigationState.CurrentSection);
    }

    [Fact]
    public async Task Webhook_ShouldAutoProcessSingleImportCandidateInChat_WithoutSelectionPrompt()
    {
        var orchestrator = new FakeConversationOrchestrator
        {
            NextResult = ConversationAgentResult.Empty(
                agentName: "assistant-agent",
                intent: "assistant.vocabulary.import.source.url",
                message: "Send URL.")
        };
        var discovery = new FakeVocabularyDiscoveryService
        {
            NextResult = new VocabularyDiscoveryResult(
                VocabularyDiscoveryStatus.Success,
                [new VocabularyDiscoveryCandidate("guilty", "adj", 3)],
                "ok",
                SourceWasUrl: true)
        };
        var sender = new FakeTelegramBotSender();
        var navigationState = new FakeNavigationStateService { CurrentSection = NavigationSections.Main };
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
            sender,
            new TelegramOptions { Enabled = true },
            vocabularyDiscoveryService: discovery);

        _ = await sut.Webhook(BuildTextUpdate(1001, 2002, "Chat", null, updateId: 211), CancellationToken.None);
        _ = await sut.Webhook(BuildTextUpdate(1001, 2002, "Імпорт з посилання", null, updateId: 212), CancellationToken.None);

        orchestrator.NextResult = BuildVocabularySingleResult();

        var response = await sut.Webhook(BuildTextUpdate(1001, 2002, "https://example.com/article", null, updateId: 213), CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);

        Assert.True(payload.Replied);
        Assert.Equal("vocabulary.single", payload.Intent);
        Assert.Equal("guilty", orchestrator.LastInput);
        Assert.DoesNotContain("Reply with numbers", sender.LastText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(NavigationSections.Chat, navigationState.CurrentSection);
    }

    [Fact]
    public async Task Webhook_ShouldStartUrlImportFlowFromNaturalChatPhrase_WhenAssistantIntentIsOff()
    {
        var orchestrator = new FakeConversationOrchestrator
        {
            NextResult = ConversationAgentResult.Empty(
                agentName: "assistant-agent",
                intent: "assistant.settings.open",
                message: "Opening settings.")
        };
        var sender = new FakeTelegramBotSender();
        var discovery = new FakeVocabularyDiscoveryService();
        var navigationState = new FakeNavigationStateService { CurrentSection = NavigationSections.Main };

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
            new FakeTelegramFormatter("formatted"),
            sender,
            new TelegramOptions { Enabled = true },
            vocabularyDiscoveryService: discovery);

        _ = await sut.Webhook(BuildTextUpdate(1001, 2002, "Chat", null, updateId: 231), CancellationToken.None);

        var startImportResponse = await sut.Webhook(
            BuildTextUpdate(1001, 2002, "Зараз скину тобі посилання. Зробиш імпорт?", null, updateId: 232),
            CancellationToken.None);

        var startImportOk = Assert.IsType<OkObjectResult>(startImportResponse.Result);
        var startImportPayload = Assert.IsType<TelegramWebhookResponse>(startImportOk.Value);
        Assert.True(startImportPayload.Replied);
        Assert.Equal("vocab.import.source", startImportPayload.Intent);
        Assert.Contains("Send URL", sender.LastText, StringComparison.OrdinalIgnoreCase);

        var urlResponse = await sut.Webhook(
            BuildTextUpdate(1001, 2002, "https://example.com/article", null, updateId: 233),
            CancellationToken.None);

        var urlOk = Assert.IsType<OkObjectResult>(urlResponse.Result);
        var urlPayload = Assert.IsType<TelegramWebhookResponse>(urlOk.Value);
        Assert.True(urlPayload.Replied);
        Assert.Equal("vocab.url.suggestions", urlPayload.Intent);
        Assert.Equal(1, discovery.Calls);
        Assert.Equal("https://example.com/article", discovery.LastSourceInput);
        Assert.Equal(0, orchestrator.Calls);
        Assert.Equal(NavigationSections.Chat, navigationState.CurrentSection);
    }

    [Theory]
    [InlineData("Зараз скину фото для імпорту", "Send photo")]
    [InlineData("Я надішлю файл для імпорту", "Send file")]
    [InlineData("Скину текст для імпорту", "Send text")]
    [InlineData("Скину тобі посилання. Зробиш імпорт?", "Send URL")]
    public async Task Webhook_ShouldSelectCorrectImportSourceFromNaturalChatPhrase(
        string chatPhrase,
        string expectedPrompt)
    {
        var orchestrator = new FakeConversationOrchestrator
        {
            NextResult = ConversationAgentResult.Empty(
                agentName: "assistant-agent",
                intent: "assistant.settings.open",
                message: "Opening settings.")
        };
        var sender = new FakeTelegramBotSender();
        var navigationState = new FakeNavigationStateService { CurrentSection = NavigationSections.Main };

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
            new FakeTelegramFormatter("formatted"),
            sender,
            new TelegramOptions { Enabled = true });

        _ = await sut.Webhook(BuildTextUpdate(1001, 2002, "Chat", null, updateId: 241), CancellationToken.None);

        var response = await sut.Webhook(
            BuildTextUpdate(1001, 2002, chatPhrase, null, updateId: 242),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);
        Assert.True(payload.Replied);
        Assert.Equal("vocab.import.source", payload.Intent);
        Assert.Contains(expectedPrompt, sender.LastText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, orchestrator.Calls);
        Assert.Equal(NavigationSections.Chat, navigationState.CurrentSection);
    }

    [Fact]
    public async Task Webhook_ShouldProcessTextImportAfterNaturalChatPhrase()
    {
        var orchestrator = new FakeConversationOrchestrator
        {
            NextResult = ConversationAgentResult.Empty(
                agentName: "assistant-agent",
                intent: "assistant.settings.open",
                message: "Opening settings.")
        };
        var sender = new FakeTelegramBotSender();
        var discovery = new FakeVocabularyDiscoveryService();
        var navigationState = new FakeNavigationStateService { CurrentSection = NavigationSections.Main };

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
            new FakeTelegramFormatter("formatted"),
            sender,
            new TelegramOptions { Enabled = true },
            vocabularyDiscoveryService: discovery);

        _ = await sut.Webhook(BuildTextUpdate(1001, 2002, "Chat", null, updateId: 251), CancellationToken.None);
        _ = await sut.Webhook(BuildTextUpdate(1001, 2002, "Скину текст для імпорту", null, updateId: 252), CancellationToken.None);

        var response = await sut.Webhook(
            BuildTextUpdate(1001, 2002, "The architecture helps deploy scalable services.", null, updateId: 253),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);
        Assert.True(payload.Replied);
        Assert.Equal("vocab.url.suggestions", payload.Intent);
        Assert.Equal(1, discovery.Calls);
        Assert.Equal("The architecture helps deploy scalable services.", discovery.LastSourceInput);
        Assert.Equal(0, orchestrator.Calls);
    }

    [Fact]
    public async Task Webhook_ShouldProcessFileImportAfterNaturalChatPhrase()
    {
        var orchestrator = new FakeConversationOrchestrator
        {
            NextResult = ConversationAgentResult.Empty(
                agentName: "assistant-agent",
                intent: "assistant.settings.open",
                message: "Opening settings.")
        };
        var sender = new FakeTelegramBotSender();
        var discovery = new FakeVocabularyDiscoveryService();
        var importReader = new FakeTelegramImportSourceReader
        {
            NextResult = new TelegramImportSourceReadResult(
                TelegramImportSourceReadStatus.Success,
                "The architecture helps deploy scalable services.")
        };
        var navigationState = new FakeNavigationStateService { CurrentSection = NavigationSections.Main };

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
            new FakeTelegramFormatter("formatted"),
            sender,
            new TelegramOptions { Enabled = true },
            vocabularyDiscoveryService: discovery,
            importSourceReader: importReader);

        _ = await sut.Webhook(BuildTextUpdate(1001, 2002, "Chat", null, updateId: 261), CancellationToken.None);
        _ = await sut.Webhook(BuildTextUpdate(1001, 2002, "Надішлю файл для імпорту", null, updateId: 262), CancellationToken.None);

        var response = await sut.Webhook(
            BuildDocumentUpdate(1001, 2002, "file-123", "words.pdf", "application/pdf", null, updateId: 263),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);
        Assert.True(payload.Replied);
        Assert.Equal("vocab.url.suggestions", payload.Intent);
        Assert.Equal(1, discovery.Calls);
        Assert.Equal(TelegramImportSourceType.File, importReader.LastSourceType);
        Assert.Equal(0, orchestrator.Calls);
    }

    [Fact]
    public async Task Webhook_ShouldProcessPhotoImportAfterNaturalChatPhrase()
    {
        var orchestrator = new FakeConversationOrchestrator
        {
            NextResult = ConversationAgentResult.Empty(
                agentName: "assistant-agent",
                intent: "assistant.settings.open",
                message: "Opening settings.")
        };
        var sender = new FakeTelegramBotSender();
        var discovery = new FakeVocabularyDiscoveryService();
        var importReader = new FakeTelegramImportSourceReader
        {
            NextResult = new TelegramImportSourceReadResult(
                TelegramImportSourceReadStatus.Success,
                "The architecture helps deploy scalable services.")
        };
        var navigationState = new FakeNavigationStateService { CurrentSection = NavigationSections.Main };

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
            new FakeTelegramFormatter("formatted"),
            sender,
            new TelegramOptions { Enabled = true },
            vocabularyDiscoveryService: discovery,
            importSourceReader: importReader);

        _ = await sut.Webhook(BuildTextUpdate(1001, 2002, "Chat", null, updateId: 271), CancellationToken.None);
        _ = await sut.Webhook(BuildTextUpdate(1001, 2002, "Скину фото для імпорту", null, updateId: 272), CancellationToken.None);

        var response = await sut.Webhook(
            BuildPhotoUpdate(1001, 2002, "photo-abc", null, updateId: 273),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);
        Assert.True(payload.Replied);
        Assert.Equal("vocab.url.suggestions", payload.Intent);
        Assert.Equal(1, discovery.Calls);
        Assert.Equal(TelegramImportSourceType.Photo, importReader.LastSourceType);
        Assert.Equal(0, orchestrator.Calls);
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

    [Fact]
    public async Task Webhook_ShouldBuildUrlSuggestions_WhenFromUrlModeIsActive()
    {
        var orchestrator = new FakeConversationOrchestrator();
        var discovery = new FakeVocabularyDiscoveryService();
        var sender = new FakeTelegramBotSender();

        var sut = CreateSut(
            orchestrator,
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            new FakeTelegramFormatter("ignored"),
            sender,
            new TelegramOptions { Enabled = true },
            vocabularyDiscoveryService: discovery);

        await sut.Webhook(BuildCallbackUpdate(1001, 2002, CallbackDataConstants.Vocab.Url, null), CancellationToken.None);
        var response = await sut.Webhook(BuildTextUpdate(1001, 2002, "https://example.com/article", null), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);

        Assert.True(payload.Replied);
        Assert.Equal("vocab.url.suggestions", payload.Intent);
        Assert.Equal(1, discovery.Calls);
        Assert.Equal("https://example.com/article", discovery.LastSourceInput);
        Assert.Contains("Suggested new words", sender.LastText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("1)", sender.LastText, StringComparison.Ordinal);
        Assert.Equal(0, orchestrator.Calls);
    }

    [Fact]
    public async Task Webhook_ShouldShowImportSourceMenu_WhenImportCallbackClicked()
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

        var response = await sut.Webhook(
            BuildCallbackUpdate(1001, 2002, CallbackDataConstants.Vocab.Url, null),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);
        Assert.True(payload.Replied);
        Assert.Equal("vocab.import", payload.Intent);
        Assert.Contains("Choose import source", sender.LastText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Webhook_ShouldWarnAboutExpectedFile_WhenFileSourceSelectedButTextSent()
    {
        const long chatId = 701001;
        const long userId = 702002;
        var sender = new FakeTelegramBotSender();
        var importSourceReader = new FakeTelegramImportSourceReader
        {
            NextResult = new TelegramImportSourceReadResult(TelegramImportSourceReadStatus.WrongInputType)
        };
        var sut = CreateSut(
            new FakeConversationOrchestrator(),
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            new FakeTelegramFormatter("ignored"),
            sender,
            new TelegramOptions { Enabled = true },
            importSourceReader: importSourceReader);

        await sut.Webhook(BuildCallbackUpdate(chatId, userId, CallbackDataConstants.Vocab.Url, null), CancellationToken.None);
        await sut.Webhook(BuildCallbackUpdate(chatId, userId, CallbackDataConstants.Vocab.ImportSourceFile, null, updateId: 61), CancellationToken.None);
        var response = await sut.Webhook(BuildTextUpdate(chatId, userId, "plain text", null, updateId: 62), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);
        Assert.True(payload.Replied);
        Assert.Equal("vocab.import.invalid", payload.Intent);
        Assert.Contains("Waiting for file", sender.LastText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Webhook_ShouldAcceptNumericSelection_ForUrlSuggestions()
    {
        var orchestrator = new FakeConversationOrchestrator
        {
            NextResult = new ConversationAgentResult(
                AgentName: "vocabulary-agent",
                Intent: "vocabulary.batch",
                IsBatch: true,
                Items: [])
        };
        var discovery = new FakeVocabularyDiscoveryService();
        var sender = new FakeTelegramBotSender();

        var sut = CreateSut(
            orchestrator,
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            new FakeTelegramFormatter("assistant reply"),
            sender,
            new TelegramOptions { Enabled = true },
            vocabularyDiscoveryService: discovery);

        await sut.Webhook(BuildCallbackUpdate(1001, 2002, CallbackDataConstants.Vocab.Url, null), CancellationToken.None);
        await sut.Webhook(BuildTextUpdate(1001, 2002, "https://example.com/article", null, updateId: 31), CancellationToken.None);
        var response = await sut.Webhook(BuildTextUpdate(1001, 2002, "1, 3", null, updateId: 32), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);

        Assert.True(payload.Replied);
        Assert.Equal("vocabulary.batch", payload.Intent);
        Assert.Equal("architecture\r\nscalable", orchestrator.LastInput);
        Assert.Equal(1, orchestrator.Calls);
        Assert.Equal("assistant reply", sender.LastText);
    }

    [Fact]
    public async Task Webhook_ShouldAutoStartUrlFlow_WhenUserSendsUrlInVocabularySection()
    {
        const long chatId = 801001;
        const long userId = 802002;
        var orchestrator = new FakeConversationOrchestrator();
        var discovery = new FakeVocabularyDiscoveryService();
        var sender = new FakeTelegramBotSender();

        var sut = CreateSut(
            orchestrator,
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            assistantSessionService: null,
            localeStateService: null,
            navigationStateService: new FakeNavigationStateService { CurrentSection = NavigationSections.Vocabulary },
            vocabularyCardRepository: null,
            navigationPresenter: null,
            new FakeTelegramFormatter("assistant reply"),
            sender,
            new TelegramOptions { Enabled = true },
            vocabularyDiscoveryService: discovery);

        var response = await sut.Webhook(BuildTextUpdate(chatId, userId, "https://example.com/page", null, updateId: 41), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);

        Assert.True(payload.Replied);
        Assert.Equal("vocab.url.suggestions", payload.Intent);
        Assert.Equal(1, discovery.Calls);
        Assert.Equal(0, orchestrator.Calls);
    }

    [Fact]
    public async Task Webhook_ShouldRejectInvalidUrlSelection_AndKeepSelectionState()
    {
        var orchestrator = new FakeConversationOrchestrator();
        var discovery = new FakeVocabularyDiscoveryService();
        var sender = new FakeTelegramBotSender();

        var sut = CreateSut(
            orchestrator,
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            new FakeTelegramFormatter("assistant reply"),
            sender,
            new TelegramOptions { Enabled = true },
            vocabularyDiscoveryService: discovery);

        await sut.Webhook(BuildCallbackUpdate(1001, 2002, CallbackDataConstants.Vocab.Url, null), CancellationToken.None);
        await sut.Webhook(BuildTextUpdate(1001, 2002, "https://example.com/article", null, updateId: 51), CancellationToken.None);
        var response = await sut.Webhook(BuildTextUpdate(1001, 2002, "999", null, updateId: 52), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);

        Assert.True(payload.Replied);
        Assert.Equal("vocab.url.selection.invalid", payload.Intent);
        Assert.Contains("Could not parse selection", sender.LastText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, orchestrator.Calls);
    }

    [Fact]
    public async Task Webhook_ShouldHandleVocabStatsCallback_AndRenderStats()
    {
        var sender = new FakeTelegramBotSender();
        var vocabRepo = new FakeVocabularyCardRepository
        {
            CountAllResult = 42,
            PartOfSpeechStatsResult =
            [
                new VocabularyPartOfSpeechStat("n", 20),
                new VocabularyPartOfSpeechStat("v", 15),
                new VocabularyPartOfSpeechStat(null, 7)
            ],
            DeckStatsResult =
            [
                new VocabularyDeckStat("wm-nouns-ua-en.xlsx", 21),
                new VocabularyDeckStat("wm-verbs-us-en.xlsx", 14),
                new VocabularyDeckStat("wm-phrasal-verbs-ua-en.xlsx", 7)
            ]
        };

        var sut = CreateSut(
            new FakeConversationOrchestrator(),
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            assistantSessionService: null,
            localeStateService: new FakeUserLocaleStateService { StoredLocale = "en", NextLocale = "en" },
            navigationStateService: new FakeNavigationStateService { CurrentSection = NavigationSections.Vocabulary },
            vocabularyCardRepository: vocabRepo,
            navigationPresenter: null,
            new FakeTelegramFormatter("ignored"),
            sender,
            new TelegramOptions { Enabled = true });

        var response = await sut.Webhook(
            BuildCallbackUpdate(1001, 2002, CallbackDataConstants.Vocab.Stats, null),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);

        Assert.True(payload.Replied);
        Assert.Equal("vocab.stats", payload.Intent);
        Assert.Contains("Vocabulary statistics", sender.LastText, StringComparison.Ordinal);
        Assert.Contains("Total indexed words: 42", sender.LastText, StringComparison.Ordinal);
        Assert.Contains("Nouns: 20", sender.LastText, StringComparison.Ordinal);
        Assert.Contains("Verbs: 15", sender.LastText, StringComparison.Ordinal);
        Assert.Contains("... and 7 more", sender.LastText, StringComparison.Ordinal);
        Assert.Contains("wm-nouns-ua-en.xlsx", sender.LastText, StringComparison.Ordinal);
        Assert.DoesNotContain("(unclassified)", sender.LastText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Webhook_ShouldHandleVocabStatsCallback_AndRenderLocalizedPrimaryPartOfSpeechGroups()
    {
        var sender = new FakeTelegramBotSender();
        var vocabRepo = new FakeVocabularyCardRepository
        {
            CountAllResult = 2703,
            PartOfSpeechStatsResult =
            [
                new VocabularyPartOfSpeechStat("n", 1116),
                new VocabularyPartOfSpeechStat("v", 637),
                new VocabularyPartOfSpeechStat("pv", 213),
                new VocabularyPartOfSpeechStat("iv", 140),
                new VocabularyPartOfSpeechStat("adv", 122),
                new VocabularyPartOfSpeechStat("prep", 81),
                new VocabularyPartOfSpeechStat("adj", 511),
                new VocabularyPartOfSpeechStat(null, 723)
            ],
            DeckStatsResult =
            [
                new VocabularyDeckStat("wm-nouns-ua-en.xlsx", 837),
                new VocabularyDeckStat("wm-verbs-us-en.xlsx", 622)
            ]
        };

        var sut = CreateSut(
            new FakeConversationOrchestrator(),
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            assistantSessionService: null,
            localeStateService: new FakeUserLocaleStateService { StoredLocale = "uk", NextLocale = "uk" },
            navigationStateService: new FakeNavigationStateService { CurrentSection = NavigationSections.Vocabulary },
            vocabularyCardRepository: vocabRepo,
            navigationPresenter: null,
            new FakeTelegramFormatter("ignored"),
            sender,
            new TelegramOptions { Enabled = true });

        var response = await sut.Webhook(
            BuildCallbackUpdate(1001, 2002, CallbackDataConstants.Vocab.Stats, null),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);

        Assert.True(payload.Replied);
        Assert.Equal("vocab.stats", payload.Intent);
        Assert.Contains("Іменники: 1116", sender.LastText, StringComparison.Ordinal);
        Assert.Contains("Дієслова: 637", sender.LastText, StringComparison.Ordinal);
        Assert.Contains("Фразові дієслова: 213", sender.LastText, StringComparison.Ordinal);
        Assert.Contains("Неправильні дієслова: 140", sender.LastText, StringComparison.Ordinal);
        Assert.Contains("Прислівники: 122", sender.LastText, StringComparison.Ordinal);
        Assert.Contains("Прийменники: 81", sender.LastText, StringComparison.Ordinal);
        Assert.Contains("... і ще 1234", sender.LastText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Webhook_ShouldHandleVocabStatsCallback_AndShowEmptyState()
    {
        var sender = new FakeTelegramBotSender();
        var vocabRepo = new FakeVocabularyCardRepository
        {
            CountAllResult = 0
        };

        var sut = CreateSut(
            new FakeConversationOrchestrator(),
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            assistantSessionService: null,
            localeStateService: new FakeUserLocaleStateService { StoredLocale = "en", NextLocale = "en" },
            navigationStateService: new FakeNavigationStateService { CurrentSection = NavigationSections.Vocabulary },
            vocabularyCardRepository: vocabRepo,
            navigationPresenter: null,
            new FakeTelegramFormatter("ignored"),
            sender,
            new TelegramOptions { Enabled = true });

        var response = await sut.Webhook(
            BuildCallbackUpdate(1001, 2002, CallbackDataConstants.Vocab.Stats, null),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);

        Assert.True(payload.Replied);
        Assert.Equal("vocab.stats", payload.Intent);
        Assert.Contains("Stats empty", sender.LastText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Webhook_ShouldHandleVocabStatsCallback_AndReturnFallback_WhenStatsBuildingFails()
    {
        var sender = new FakeTelegramBotSender();
        var vocabRepo = new FakeVocabularyCardRepository
        {
            CountAllResult = 42,
            ThrowOnGetDeckStats = true
        };

        var sut = CreateSut(
            new FakeConversationOrchestrator(),
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            assistantSessionService: null,
            localeStateService: new FakeUserLocaleStateService { StoredLocale = "en", NextLocale = "en" },
            navigationStateService: new FakeNavigationStateService { CurrentSection = NavigationSections.Vocabulary },
            vocabularyCardRepository: vocabRepo,
            navigationPresenter: null,
            new FakeTelegramFormatter("ignored"),
            sender,
            new TelegramOptions { Enabled = true });

        var response = await sut.Webhook(
            BuildCallbackUpdate(1001, 2002, CallbackDataConstants.Vocab.Stats, null),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);

        Assert.True(payload.Replied);
        Assert.Equal("vocab.stats.failed", payload.Intent);
        Assert.Contains("Operation failed", sender.LastText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Webhook_CallbackQuery_WhenRoutingThrows_StillCallsAnswerCallbackQuery()
    {
        var sender = new FakeTelegramBotSender();
        var navigationState = new FakeNavigationStateService
        {
            CurrentSection = NavigationSections.Main,
            ThrowOnSetCurrentSection = true
        };

        var sut = CreateSut(
            new FakeConversationOrchestrator(),
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            assistantSessionService: null,
            localeStateService: new FakeUserLocaleStateService { StoredLocale = LocalizationConstants.EnglishLocale },
            navigationStateService: navigationState,
            vocabularyCardRepository: null,
            navigationPresenter: null,
            new FakeTelegramFormatter("ignored"),
            sender,
            new TelegramOptions { Enabled = true });

        var response = await sut.Webhook(
            BuildCallbackUpdate(1001, 2002, CallbackDataConstants.Vocab.Add, messageThreadId: null),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);

        Assert.False(payload.Processed);
        Assert.False(payload.Replied);
        Assert.Equal(1, sender.CallbackAnswers);
        Assert.Equal("cb-1", sender.LastCallbackQueryId);
    }

    [Fact]
    public async Task Webhook_CallbackQuery_WithNullCallbackData_IsHandledSafely()
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

        var response = await sut.Webhook(
            BuildCallbackUpdate(1001, 2002, callbackData: null, messageThreadId: null),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);

        Assert.False(payload.Processed);
        Assert.False(payload.Replied);
        Assert.Equal(1, sender.CallbackAnswers);
    }

    [Fact]
    public async Task Webhook_ShouldShowOnboardingLanguagePicker_WhenStartAndLocaleMissing()
    {
        var localeState = new FakeUserLocaleStateService { StoredLocale = null, NextLocale = "en" };
        var navigationState = new FakeNavigationStateService { CurrentSection = "main" };
        var sender = new FakeTelegramBotSender();

        var sut = CreateSut(
            new FakeConversationOrchestrator(),
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            assistantSessionService: null,
            localeStateService: localeState,
            navigationStateService: navigationState,
            vocabularyCardRepository: null,
            navigationPresenter: null,
            new FakeTelegramFormatter("ignored"),
            sender,
            new TelegramOptions { Enabled = true });

        var response = await sut.Webhook(BuildTextUpdate(1001, 2002, "/start", null), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);

        Assert.True(payload.Replied);
        Assert.Equal("onboarding.language", payload.Intent);
        Assert.IsType<TelegramInlineKeyboardMarkup>(sender.LastOptions?.ReplyMarkup);
        Assert.Equal("language_onboarding", navigationState.CurrentSection);
    }

    [Fact]
    public async Task Webhook_ShouldSelectLanguageInOnboarding_AndReturnToMain()
    {
        var localeState = new FakeUserLocaleStateService { StoredLocale = null, NextLocale = "en" };
        var navigationState = new FakeNavigationStateService { CurrentSection = "language_onboarding" };
        var sender = new FakeTelegramBotSender();

        var sut = CreateSut(
            new FakeConversationOrchestrator(),
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            assistantSessionService: null,
            localeStateService: localeState,
            navigationStateService: navigationState,
            vocabularyCardRepository: null,
            navigationPresenter: null,
            new FakeTelegramFormatter("ignored"),
            sender,
            new TelegramOptions { Enabled = true });

        var response = await sut.Webhook(BuildCallbackUpdate(1001, 2002, "lang:uk", null), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);

        Assert.True(payload.Replied);
        Assert.Equal("onboarding.language.selected", payload.Intent);
        Assert.Equal("uk", localeState.StoredLocale);
        Assert.IsType<TelegramReplyKeyboardMarkup>(sender.LastOptions?.ReplyMarkup);
        Assert.Equal("main", navigationState.CurrentSection);
    }

    [Fact]
    public async Task Webhook_LanguageCallback_Lang_ru_MustNotPersistRussian()
    {
        var localeState = new FakeUserLocaleStateService { StoredLocale = null, NextLocale = LocalizationConstants.EnglishLocale };
        var navigationState = new FakeNavigationStateService { CurrentSection = NavigationSections.LanguageOnboarding };
        var sender = new FakeTelegramBotSender();

        var sut = CreateSut(
            new FakeConversationOrchestrator(),
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            assistantSessionService: null,
            localeStateService: localeState,
            navigationStateService: navigationState,
            vocabularyCardRepository: null,
            navigationPresenter: null,
            new FakeTelegramFormatter("ignored"),
            sender,
            new TelegramOptions { Enabled = true });

        var response = await sut.Webhook(
            BuildCallbackUpdate(1001, 2002, CallbackDataConstants.Lang.Russian, null),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);

        Assert.True(payload.Replied);
        Assert.Equal("onboarding.language.selected", payload.Intent);
        Assert.Equal(LocalizationConstants.UkrainianLocale, localeState.StoredLocale);
    }

    [Fact]
    public async Task Webhook_ShouldOpenSettings_WhenSettingsMainButtonPressed()
    {
        var localeState = new FakeUserLocaleStateService { StoredLocale = "en", NextLocale = "en" };
        var sender = new FakeTelegramBotSender();
        var navigationState = new FakeNavigationStateService { CurrentSection = "main" };

        var sut = CreateSut(
            new FakeConversationOrchestrator(),
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            assistantSessionService: null,
            localeStateService: localeState,
            navigationStateService: navigationState,
            vocabularyCardRepository: null,
            navigationPresenter: null,
            new FakeTelegramFormatter("ignored"),
            sender,
            new TelegramOptions { Enabled = true });

        var response = await sut.Webhook(BuildTextUpdate(1001, 2002, "Settings", null), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);

        Assert.True(payload.Replied);
        Assert.Equal("settings.section", payload.Intent);
        Assert.Equal("settings", navigationState.CurrentSection);
        Assert.IsType<TelegramInlineKeyboardMarkup>(sender.LastOptions?.ReplyMarkup);
        Assert.Contains("• <b>Language:</b> en", sender.LastText, StringComparison.Ordinal);
        Assert.Contains("• <b>OneDrive / Graph:</b> disconnected", sender.LastText, StringComparison.Ordinal);
        Assert.DoesNotContain("OneDrive / Graph: Status:", sender.LastText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Webhook_ShouldShowSaveModeScreen_WhenSettingsSaveModeCallback()
    {
        var localeState = new FakeUserLocaleStateService { StoredLocale = "en", NextLocale = "en" };
        var sender = new FakeTelegramBotSender();

        var sut = CreateSut(
            new FakeConversationOrchestrator(),
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            assistantSessionService: null,
            localeStateService: localeState,
            navigationStateService: new FakeNavigationStateService { CurrentSection = "settings" },
            vocabularyCardRepository: null,
            navigationPresenter: null,
            new FakeTelegramFormatter("ignored"),
            sender,
            new TelegramOptions { Enabled = true });

        var response = await sut.Webhook(BuildCallbackUpdate(1001, 2002, "settings:savemode", null), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);

        Assert.True(payload.Replied);
        Assert.Equal("settings.savemode", payload.Intent);
    }

    [Fact]
    public async Task Webhook_ShouldAskForSave_WhenSaveModeAskAndCardReady()
    {
        var orchestrator = new FakeConversationOrchestrator
        {
            NextResult = BuildVocabularySingleResult()
        };
        var persistence = new FakeVocabularyPersistenceService();
        var sender = new FakeTelegramBotSender();

        var sut = CreateSut(
            orchestrator,
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            assistantSessionService: null,
            localeStateService: new FakeUserLocaleStateService { StoredLocale = "en", NextLocale = "en" },
            navigationStateService: new FakeNavigationStateService { CurrentSection = NavigationSections.Vocabulary },
            vocabularyCardRepository: null,
            navigationPresenter: null,
            new FakeTelegramFormatter("assistant reply"),
            sender,
            new TelegramOptions { Enabled = true },
            processedUpdates: null,
            vocabularyPersistenceService: persistence);

        var response = await sut.Webhook(
            BuildTextUpdate(1001, 2002, "smile", null),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);
        Assert.True(payload.Replied);
        Assert.Equal("vocabulary.single", payload.Intent);
        Assert.Equal(0, persistence.Calls);
        Assert.Contains("❓ Save &quot;smile&quot; to &quot;wm-verbs-us-en.xlsx&quot;?", sender.LastText, StringComparison.Ordinal);

        var keyboard = Assert.IsType<TelegramInlineKeyboardMarkup>(sender.LastOptions?.ReplyMarkup);
        var callbackData = keyboard.InlineKeyboard.SelectMany(row => row).Select(button => button.CallbackData).ToList();
        Assert.Contains(CallbackDataConstants.Vocab.SaveYes, callbackData);
        Assert.Contains(CallbackDataConstants.Vocab.SaveNo, callbackData);
    }

    [Fact]
    public async Task Webhook_ShouldSavePendingCard_WhenSaveConfirmationCallbackReceived()
    {
        var orchestrator = new FakeConversationOrchestrator
        {
            NextResult = BuildVocabularySingleResult()
        };
        var persistence = new FakeVocabularyPersistenceService();
        var sender = new FakeTelegramBotSender();

        var sut = CreateSut(
            orchestrator,
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            assistantSessionService: null,
            localeStateService: new FakeUserLocaleStateService { StoredLocale = "en", NextLocale = "en" },
            navigationStateService: new FakeNavigationStateService { CurrentSection = NavigationSections.Vocabulary },
            vocabularyCardRepository: null,
            navigationPresenter: null,
            new FakeTelegramFormatter("assistant reply"),
            sender,
            new TelegramOptions { Enabled = true },
            processedUpdates: null,
            vocabularyPersistenceService: persistence);

        await sut.Webhook(BuildTextUpdate(1001, 2002, "smile", null), CancellationToken.None);
        var response = await sut.Webhook(
            BuildCallbackUpdate(1001, 2002, CallbackDataConstants.Vocab.SaveYes, null),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);
        Assert.True(payload.Replied);
        Assert.Equal("vocab.save.done", payload.Intent);
        Assert.Equal(1, persistence.Calls);
        Assert.Contains("Saved to", sender.LastText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Webhook_ShouldAskBatchSaveWithSeparatorQuestionAndButtons_WhenSaveModeAsk()
    {
        var orchestrator = new FakeConversationOrchestrator
        {
            NextResult = BuildVocabularyBatchSavableResult()
        };
        var persistence = new FakeVocabularyPersistenceService();
        var sender = new FakeTelegramBotSender();

        var sut = CreateSut(
            orchestrator,
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            assistantSessionService: null,
            localeStateService: new FakeUserLocaleStateService { StoredLocale = "en", NextLocale = "en" },
            navigationStateService: new FakeNavigationStateService { CurrentSection = NavigationSections.Vocabulary },
            vocabularyCardRepository: null,
            navigationPresenter: null,
            new FakeTelegramFormatter("batch body"),
            sender,
            new TelegramOptions { Enabled = true },
            processedUpdates: null,
            vocabularyPersistenceService: persistence);

        var response = await sut.Webhook(
            BuildTextUpdate(1001, 2002, "awkward exact", null),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);
        Assert.True(payload.Replied);
        Assert.Equal("vocabulary.batch", payload.Intent);
        Assert.Equal(0, persistence.Calls);

        var normalized = sender.LastText.Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.Contains("--------------------\nℹ️ Batch ask hint", normalized, StringComparison.Ordinal);
        Assert.Contains("❓ Save all 2 new items from this batch?", normalized, StringComparison.Ordinal);

        var keyboard = Assert.IsType<TelegramInlineKeyboardMarkup>(sender.LastOptions?.ReplyMarkup);
        var callbackData = keyboard.InlineKeyboard.SelectMany(row => row).Select(button => button.CallbackData).ToList();
        Assert.Contains(CallbackDataConstants.Vocab.SaveBatchYes, callbackData);
        Assert.Contains(CallbackDataConstants.Vocab.SaveBatchNo, callbackData);
    }

    [Fact]
    public async Task Webhook_ShouldSaveAllPendingBatchCards_WhenBatchSaveConfirmationCallbackReceived()
    {
        var orchestrator = new FakeConversationOrchestrator
        {
            NextResult = BuildVocabularyBatchSavableResult()
        };
        var persistence = new FakeVocabularyPersistenceService();
        var sender = new FakeTelegramBotSender();

        var sut = CreateSut(
            orchestrator,
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            assistantSessionService: null,
            localeStateService: new FakeUserLocaleStateService { StoredLocale = "en", NextLocale = "en" },
            navigationStateService: new FakeNavigationStateService { CurrentSection = NavigationSections.Vocabulary },
            vocabularyCardRepository: null,
            navigationPresenter: null,
            new FakeTelegramFormatter("batch body"),
            sender,
            new TelegramOptions { Enabled = true },
            processedUpdates: null,
            vocabularyPersistenceService: persistence);

        await sut.Webhook(BuildTextUpdate(1001, 2002, "awkward exact", null), CancellationToken.None);
        var response = await sut.Webhook(
            BuildCallbackUpdate(1001, 2002, CallbackDataConstants.Vocab.SaveBatchYes, null, updateId: 77),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);
        Assert.True(payload.Replied);
        Assert.Equal("vocab.save.batch.done", payload.Intent);
        Assert.Equal(2, persistence.Calls);
        Assert.Contains("saved=2", sender.LastText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Webhook_ShouldSwitchStorageModeToGraph_WhenOneDriveLoginCompleted()
    {
        var storagePreference = new FakeVocabularyStoragePreferenceService
        {
            CurrentMode = VocabularyStorageMode.Local
        };
        var syncProcessor = new FakeVocabularySyncProcessor
        {
            NextSummary = new VocabularySyncRunSummary(
                Requested: 1,
                Processed: 1,
                Completed: 1,
                Requeued: 0,
                Failed: 0,
                PendingAfterRun: 0)
        };
        var sender = new FakeTelegramBotSender();

        var sut = CreateSut(
            new FakeConversationOrchestrator(),
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            storagePreference,
            assistantSessionService: null,
            localeStateService: new FakeUserLocaleStateService { StoredLocale = "en", NextLocale = "en" },
            navigationStateService: new FakeNavigationStateService { CurrentSection = NavigationSections.Settings },
            vocabularyCardRepository: null,
            navigationPresenter: null,
            new FakeTelegramFormatter("ignored"),
            sender,
            new TelegramOptions { Enabled = true },
            vocabularySyncProcessor: syncProcessor);

        await sut.Webhook(
            BuildCallbackUpdate(1001, 2002, CallbackDataConstants.OneDrive.Login, null, updateId: 21),
            CancellationToken.None);

        var response = await sut.Webhook(
            BuildCallbackUpdate(1001, 2002, CallbackDataConstants.OneDrive.CheckLogin, null, updateId: 22),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);

        Assert.True(payload.Replied);
        Assert.Equal("settings.onedrive.check.success", payload.Intent);
        Assert.Equal(VocabularyStorageMode.Graph, storagePreference.CurrentMode);
        Assert.Contains("switched", sender.LastText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, syncProcessor.ProcessCalls);
    }

    [Fact]
    public async Task Webhook_ShouldSuggestIndexRebuild_WhenLoginCompletedAndIndexIsEmpty()
    {
        var storagePreference = new FakeVocabularyStoragePreferenceService
        {
            CurrentMode = VocabularyStorageMode.Local
        };
        var syncProcessor = new FakeVocabularySyncProcessor
        {
            NextSummary = new VocabularySyncRunSummary(
                Requested: 0,
                Processed: 0,
                Completed: 0,
                Requeued: 0,
                Failed: 0,
                PendingAfterRun: 0)
        };
        var sender = new FakeTelegramBotSender();

        var sut = CreateSut(
            new FakeConversationOrchestrator(),
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            storagePreference,
            assistantSessionService: null,
            localeStateService: new FakeUserLocaleStateService { StoredLocale = "en", NextLocale = "en" },
            navigationStateService: new FakeNavigationStateService { CurrentSection = NavigationSections.Settings },
            vocabularyCardRepository: new FakeVocabularyCardRepository(),
            navigationPresenter: null,
            new FakeTelegramFormatter("ignored"),
            sender,
            new TelegramOptions { Enabled = true },
            vocabularySyncProcessor: syncProcessor);

        await sut.Webhook(
            BuildCallbackUpdate(1001, 2002, CallbackDataConstants.OneDrive.Login, null, updateId: 31),
            CancellationToken.None);

        var response = await sut.Webhook(
            BuildCallbackUpdate(1001, 2002, CallbackDataConstants.OneDrive.CheckLogin, null, updateId: 32),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);

        Assert.True(payload.Replied);
        Assert.Equal("settings.onedrive.check.success", payload.Intent);
        Assert.Contains("cache appears empty", sender.LastText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Webhook_ShouldReportIndexReady_WhenLoginCompletedAndIndexAlreadyPopulated()
    {
        var storagePreference = new FakeVocabularyStoragePreferenceService
        {
            CurrentMode = VocabularyStorageMode.Local
        };
        var syncProcessor = new FakeVocabularySyncProcessor
        {
            NextSummary = new VocabularySyncRunSummary(
                Requested: 0,
                Processed: 0,
                Completed: 0,
                Requeued: 0,
                Failed: 0,
                PendingAfterRun: 0)
        };
        var sender = new FakeTelegramBotSender();

        var sut = CreateSut(
            new FakeConversationOrchestrator(),
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            storagePreference,
            assistantSessionService: null,
            localeStateService: new FakeUserLocaleStateService { StoredLocale = "en", NextLocale = "en" },
            navigationStateService: new FakeNavigationStateService { CurrentSection = NavigationSections.Settings },
            vocabularyCardRepository: new FakeVocabularyCardRepository
            {
                CountAllResult = 3987
            },
            navigationPresenter: null,
            new FakeTelegramFormatter("ignored"),
            sender,
            new TelegramOptions { Enabled = true },
            vocabularySyncProcessor: syncProcessor);

        await sut.Webhook(
            BuildCallbackUpdate(1001, 2002, CallbackDataConstants.OneDrive.Login, null, updateId: 33),
            CancellationToken.None);

        var response = await sut.Webhook(
            BuildCallbackUpdate(1001, 2002, CallbackDataConstants.OneDrive.CheckLogin, null, updateId: 34),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);

        Assert.True(payload.Replied);
        Assert.Equal("settings.onedrive.check.success", payload.Intent);
        Assert.Contains("index is ready", sender.LastText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("3987", sender.LastText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cache appears empty", sender.LastText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Webhook_ShouldRunPendingSync_WhenOneDriveSyncNowCallbackReceived()
    {
        var sender = new FakeTelegramBotSender();
        var graphAuth = new FakeGraphAuthService
        {
            Status = new GraphAuthStatus(true, true, "Authenticated.")
        };
        var syncProcessor = new FakeVocabularySyncProcessor
        {
            NextSummary = new VocabularySyncRunSummary(
                Requested: 2,
                Processed: 2,
                Completed: 2,
                Requeued: 0,
                Failed: 0,
                PendingAfterRun: 0)
        };

        var sut = CreateSut(
            new FakeConversationOrchestrator(),
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            assistantSessionService: null,
            localeStateService: new FakeUserLocaleStateService { StoredLocale = "en", NextLocale = "en" },
            navigationStateService: new FakeNavigationStateService { CurrentSection = NavigationSections.Settings },
            vocabularyCardRepository: null,
            navigationPresenter: null,
            new FakeTelegramFormatter("ignored"),
            sender,
            new TelegramOptions { Enabled = true },
            graphAuthService: graphAuth,
            vocabularySyncProcessor: syncProcessor);

        var response = await sut.Webhook(
            BuildCallbackUpdate(1001, 2002, CallbackDataConstants.OneDrive.SyncNow, null),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);

        Assert.True(payload.Replied);
        Assert.Equal("settings.onedrive.sync.done", payload.Intent);
        Assert.Equal(1, syncProcessor.ProcessCalls);
        Assert.Contains("Sync complete", sender.LastText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Webhook_ShouldConfirmAndRebuildIndex_WhenOneDriveRebuildIndexCallbackReceived()
    {
        var sender = new FakeTelegramBotSender();
        var graphAuth = new FakeGraphAuthService
        {
            Status = new GraphAuthStatus(true, true, "Authenticated.")
        };
        var deckService = new FakeVocabularyDeckService
        {
            AllEntries =
            [
                new VocabularyDeckEntry("wm-verbs-us-en.xlsx", "/apps/Flashcards Deluxe/wm-verbs-us-en.xlsx", 10, "work", "(v) працювати", "I work."),
                new VocabularyDeckEntry("wm-nouns-ua-en.xlsx", "/apps/Flashcards Deluxe/wm-nouns-ua-en.xlsx", 20, "work", "(n) робота", "The work is hard.")
            ]
        };
        var indexService = new FakeVocabularyIndexService
        {
            RebuildResult = 2
        };

        var sut = CreateSut(
            new FakeConversationOrchestrator(),
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            assistantSessionService: null,
            localeStateService: new FakeUserLocaleStateService { StoredLocale = "en", NextLocale = "en" },
            navigationStateService: new FakeNavigationStateService { CurrentSection = NavigationSections.Settings },
            vocabularyCardRepository: null,
            navigationPresenter: null,
            new FakeTelegramFormatter("ignored"),
            sender,
            new TelegramOptions { Enabled = true },
            graphAuthService: graphAuth,
            vocabularyIndexService: indexService,
            vocabularyDeckService: deckService);

        var confirmResponse = await sut.Webhook(
            BuildCallbackUpdate(1001, 2002, CallbackDataConstants.OneDrive.RebuildIndex, null),
            CancellationToken.None);

        var confirmOk = Assert.IsType<OkObjectResult>(confirmResponse.Result);
        var confirmPayload = Assert.IsType<TelegramWebhookResponse>(confirmOk.Value);

        Assert.True(confirmPayload.Replied);
        Assert.Equal("settings.onedrive.index.confirm", confirmPayload.Intent);
        Assert.Equal(0, indexService.RebuildCalls);
        Assert.Contains($"❓{Environment.NewLine}⚠️ Rebuilding cache can take some time. Start now?", sender.LastText, StringComparison.Ordinal);
        Assert.Contains("take some time", sender.LastText, StringComparison.OrdinalIgnoreCase);

        var response = await sut.Webhook(
            BuildCallbackUpdate(1001, 2002, CallbackDataConstants.OneDrive.RebuildIndexConfirm, null, updateId: 3),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);

        Assert.True(payload.Replied);
        Assert.Equal("settings.onedrive.index.done", payload.Intent);
        Assert.Equal(1, indexService.RebuildCalls);
        Assert.Equal(VocabularyStorageMode.Graph, indexService.LastMode);
        Assert.True(sender.SentMessages.Count >= 3);
        Assert.Contains("Rebuilding cache started", sender.SentMessages[^2].Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Cache rebuilt", sender.LastText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Webhook_ShouldConfirmAndClearCache_WhenOneDriveClearCacheCallbackReceived()
    {
        var sender = new FakeTelegramBotSender();
        var indexService = new FakeVocabularyIndexService
        {
            ClearResult = 12
        };
        var vocabRepo = new FakeVocabularyCardRepository
        {
            CountAllResult = 57
        };

        var sut = CreateSut(
            new FakeConversationOrchestrator(),
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            assistantSessionService: null,
            localeStateService: new FakeUserLocaleStateService { StoredLocale = "en", NextLocale = "en" },
            navigationStateService: new FakeNavigationStateService { CurrentSection = NavigationSections.Settings },
            vocabularyCardRepository: vocabRepo,
            navigationPresenter: null,
            new FakeTelegramFormatter("ignored"),
            sender,
            new TelegramOptions { Enabled = true },
            vocabularyIndexService: indexService);

        var confirmResponse = await sut.Webhook(
            BuildCallbackUpdate(1001, 2002, CallbackDataConstants.OneDrive.ClearCache, null, updateId: 11),
            CancellationToken.None);

        var confirmOk = Assert.IsType<OkObjectResult>(confirmResponse.Result);
        var confirmPayload = Assert.IsType<TelegramWebhookResponse>(confirmOk.Value);

        Assert.True(confirmPayload.Replied);
        Assert.Equal("settings.onedrive.cache.confirm", confirmPayload.Intent);
        Assert.Contains($"❓{Environment.NewLine}⚠️ Clear cache?", sender.LastText, StringComparison.Ordinal);
        Assert.Contains("records=57", sender.LastText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, indexService.ClearCalls);

        var doneResponse = await sut.Webhook(
            BuildCallbackUpdate(1001, 2002, CallbackDataConstants.OneDrive.ClearCacheConfirm, null, updateId: 12),
            CancellationToken.None);

        var doneOk = Assert.IsType<OkObjectResult>(doneResponse.Result);
        var donePayload = Assert.IsType<TelegramWebhookResponse>(doneOk.Value);

        Assert.True(donePayload.Replied);
        Assert.Equal("settings.onedrive.cache.done", donePayload.Intent);
        Assert.Equal(1, indexService.ClearCalls);
        Assert.Contains("Cache cleared: 12", sender.LastText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Webhook_ShouldShowOneDriveScreen_WhenSettingsOneDriveCallback()
    {
        var localeState = new FakeUserLocaleStateService { StoredLocale = "en", NextLocale = "en" };
        var sender = new FakeTelegramBotSender();

        var sut = CreateSut(
            new FakeConversationOrchestrator(),
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            assistantSessionService: null,
            localeStateService: localeState,
            navigationStateService: new FakeNavigationStateService { CurrentSection = "settings" },
            vocabularyCardRepository: null,
            navigationPresenter: null,
            new FakeTelegramFormatter("ignored"),
            sender,
            new TelegramOptions { Enabled = true });

        var response = await sut.Webhook(BuildCallbackUpdate(1001, 2002, "settings:onedrive", null), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);

        Assert.True(payload.Replied);
        Assert.Equal("settings.onedrive", payload.Intent);
    }

    [Fact]
    public async Task Webhook_ShouldMarkQuestion_WhenOneDriveCheckLoginHasNoPendingChallenge()
    {
        var sender = new FakeTelegramBotSender();

        var sut = CreateSut(
            new FakeConversationOrchestrator(),
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            assistantSessionService: null,
            localeStateService: new FakeUserLocaleStateService { StoredLocale = "en", NextLocale = "en" },
            navigationStateService: new FakeNavigationStateService { CurrentSection = NavigationSections.Settings },
            vocabularyCardRepository: null,
            navigationPresenter: null,
            new FakeTelegramFormatter("ignored"),
            sender,
            new TelegramOptions { Enabled = true });

        var response = await sut.Webhook(
            BuildCallbackUpdate(1001, 2002, CallbackDataConstants.OneDrive.CheckLogin, null, updateId: 1144),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);

        Assert.True(payload.Replied);
        Assert.Equal("settings.onedrive.check.missing", payload.Intent);
        Assert.Contains("❓ Still not signed in", sender.LastText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Webhook_ShouldShowMissingDeckWarning_WhenOneDriveIsConnectedButConfiguredDecksAreMissing()
    {
        var localeState = new FakeUserLocaleStateService { StoredLocale = "en", NextLocale = "en" };
        var sender = new FakeTelegramBotSender();
        var graphAuth = new FakeGraphAuthService
        {
            Status = new GraphAuthStatus(true, true, "Authenticated.")
        };
        var presenter = new TelegramNavigationPresenter(new LocalizationService());
        var deckService = new FakeVocabularyDeckService
        {
            WritableDeckFiles =
            [
                new VocabularyDeckFile("wm-verbs-us-en.xlsx", "/Apps/Flashcards Deluxe/wm-verbs-us-en.xlsx")
            ]
        };

        var sut = CreateSut(
            new FakeConversationOrchestrator(),
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            assistantSessionService: null,
            localeStateService: localeState,
            navigationStateService: new FakeNavigationStateService { CurrentSection = "settings" },
            vocabularyCardRepository: null,
            navigationPresenter: presenter,
            new FakeTelegramFormatter("ignored"),
            sender,
            new TelegramOptions { Enabled = true },
            graphAuthService: graphAuth,
            vocabularyDeckService: deckService);

        var response = await sut.Webhook(
            BuildCallbackUpdate(1001, 2002, "settings:onedrive", null),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);

        Assert.True(payload.Replied);
        Assert.Equal("settings.onedrive", payload.Intent);
        Assert.Contains("Missing configured target decks", sender.LastText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("wm-nouns-ua-en.xlsx", sender.LastText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Webhook_ShouldLocalizeGraphStatusMessage_OnOneDriveScreen()
    {
        var localeState = new FakeUserLocaleStateService { StoredLocale = LocalizationConstants.UkrainianLocale, NextLocale = LocalizationConstants.UkrainianLocale };
        var sender = new FakeTelegramBotSender();
        var presenter = new TelegramNavigationPresenter(new LocalizationService());
        var graphAuth = new FakeGraphAuthService
        {
            Status = new GraphAuthStatus(true, false, "Not authenticated. Use /graph login.")
        };

        var sut = CreateSut(
            new FakeConversationOrchestrator(),
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            assistantSessionService: null,
            localeStateService: localeState,
            navigationStateService: new FakeNavigationStateService { CurrentSection = NavigationSections.Settings },
            vocabularyCardRepository: null,
            navigationPresenter: presenter,
            new FakeTelegramFormatter("ignored"),
            sender,
            new TelegramOptions { Enabled = true },
            graphAuthService: graphAuth);

        var response = await sut.Webhook(
            BuildCallbackUpdate(1001, 2002, CallbackDataConstants.Settings.OneDrive, null),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);

        Assert.True(payload.Replied);
        Assert.Equal("settings.onedrive", payload.Intent);
        Assert.Contains("Потрібна авторизація OneDrive", sender.LastText, StringComparison.Ordinal);
        Assert.DoesNotContain("Use /graph login", sender.LastText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Webhook_ShouldShowTelegramHint_WhenConsoleGraphCommandUsed()
    {
        var orchestrator = new FakeConversationOrchestrator
        {
            NextResult = ConversationAgentResult.Empty(
                "command-agent",
                "command.unsupported",
                "Unsupported command in API mode. Use natural language for vocabulary or ask for help.")
        };

        var sender = new FakeTelegramBotSender();
        var presenter = new TelegramNavigationPresenter(new LocalizationService());

        var sut = CreateSut(
            orchestrator,
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            assistantSessionService: null,
            localeStateService: new FakeUserLocaleStateService
            {
                StoredLocale = LocalizationConstants.UkrainianLocale,
                NextLocale = LocalizationConstants.UkrainianLocale
            },
            navigationStateService: new FakeNavigationStateService { CurrentSection = NavigationSections.Vocabulary },
            vocabularyCardRepository: null,
            navigationPresenter: presenter,
            new FakeTelegramFormatter("ignored"),
            sender,
            new TelegramOptions { Enabled = true });

        var response = await sut.Webhook(
            BuildTextUpdate(1001, 2002, "/graph status", null, languageCode: "uk"),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);

        Assert.True(payload.Replied);
        Assert.Equal("command.unsupported", payload.Intent);
        Assert.Contains("доступна лише в консолі", sender.LastText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OneDrive / Graph", sender.LastText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Unsupported command in API mode", sender.LastText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Webhook_ShouldQueueSaveNotice_WhenGraphAuthRequiredOnSave()
    {
        var orchestrator = new FakeConversationOrchestrator
        {
            NextResult = BuildVocabularySingleResult()
        };
        var persistence = new FakeVocabularyPersistenceService
        {
            NextResult = new VocabularyAppendResult(
                VocabularyAppendStatus.Error,
                Message: "Graph authentication is required. Run /graph login first.")
        };
        var sender = new FakeTelegramBotSender();
        var presenter = new TelegramNavigationPresenter(new LocalizationService());

        var sut = CreateSut(
            orchestrator,
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            assistantSessionService: null,
            localeStateService: new FakeUserLocaleStateService
            {
                StoredLocale = LocalizationConstants.UkrainianLocale,
                NextLocale = LocalizationConstants.UkrainianLocale
            },
            navigationStateService: new FakeNavigationStateService { CurrentSection = NavigationSections.Vocabulary },
            vocabularyCardRepository: null,
            navigationPresenter: presenter,
            new FakeTelegramFormatter("assistant reply"),
            sender,
            new TelegramOptions { Enabled = true },
            vocabularyPersistenceService: persistence);

        await sut.Webhook(BuildTextUpdate(1001, 2002, "rest", null, languageCode: "uk"), CancellationToken.None);
        var response = await sut.Webhook(
            BuildCallbackUpdate(1001, 2002, CallbackDataConstants.Vocab.SaveYes, null, languageCode: "uk"),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);

        Assert.True(payload.Replied);
        Assert.Equal("vocab.save.done", payload.Intent);
        Assert.Contains("черг", sender.LastText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Run /graph login", sender.LastText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Webhook_ShouldShowMissingDeckNoticeAndClearPendingSave_WhenTargetDeckMissingOnSave()
    {
        var orchestrator = new FakeConversationOrchestrator
        {
            NextResult = BuildVocabularySingleResult()
        };
        var persistence = new FakeVocabularyPersistenceService
        {
            NextResult = new VocabularyAppendResult(
                VocabularyAppendStatus.Error,
                Message: "Could not resolve OneDrive target deck 'wm-adjectives-ua-en.xlsx'.")
        };
        var sender = new FakeTelegramBotSender();
        var presenter = new TelegramNavigationPresenter(new LocalizationService());

        var sut = CreateSut(
            orchestrator,
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            assistantSessionService: null,
            localeStateService: new FakeUserLocaleStateService
            {
                StoredLocale = LocalizationConstants.EnglishLocale,
                NextLocale = LocalizationConstants.EnglishLocale
            },
            navigationStateService: new FakeNavigationStateService { CurrentSection = NavigationSections.Vocabulary },
            vocabularyCardRepository: null,
            navigationPresenter: presenter,
            new FakeTelegramFormatter("assistant reply"),
            sender,
            new TelegramOptions { Enabled = true },
            vocabularyPersistenceService: persistence);

        await sut.Webhook(BuildTextUpdate(1001, 2002, "awkward", null, languageCode: "en"), CancellationToken.None);
        var firstSave = await sut.Webhook(
            BuildCallbackUpdate(1001, 2002, CallbackDataConstants.Vocab.SaveYes, null, languageCode: "en"),
            CancellationToken.None);

        var firstOk = Assert.IsType<OkObjectResult>(firstSave.Result);
        var firstPayload = Assert.IsType<TelegramWebhookResponse>(firstOk.Value);
        Assert.True(firstPayload.Replied);
        Assert.Equal("vocab.save.done", firstPayload.Intent);
        Assert.Contains("missing", sender.LastText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("wm-adjectives-ua-en.xlsx", sender.LastText, StringComparison.OrdinalIgnoreCase);

        var secondSave = await sut.Webhook(
            BuildCallbackUpdate(1001, 2002, CallbackDataConstants.Vocab.SaveYes, null, languageCode: "en", updateId: 901),
            CancellationToken.None);

        var secondOk = Assert.IsType<OkObjectResult>(secondSave.Result);
        var secondPayload = Assert.IsType<TelegramWebhookResponse>(secondOk.Value);
        Assert.True(secondPayload.Replied);
        Assert.Equal("vocab.save.none", secondPayload.Intent);
    }
[Fact]
    public async Task Webhook_ShouldShowWordSuggestions_WhenWordIsUnrecognized()
    {
        var orchestrator = new FakeConversationOrchestrator
        {
            NextResult = BuildVocabularyUnrecognizedResult()
        };
        var sender = new FakeTelegramBotSender();

        var sut = CreateSut(
            orchestrator,
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            assistantSessionService: null,
            localeStateService: new FakeUserLocaleStateService
            {
                StoredLocale = LocalizationConstants.EnglishLocale,
                NextLocale = LocalizationConstants.EnglishLocale
            },
            navigationStateService: new FakeNavigationStateService { CurrentSection = NavigationSections.Vocabulary },
            vocabularyCardRepository: null,
            navigationPresenter: null,
            new FakeTelegramFormatter("Processed."),
            sender,
            new TelegramOptions { Enabled = true });

        var response = await sut.Webhook(
            BuildTextUpdate(1001, 2002, "smle", null, languageCode: "en"),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);

        Assert.True(payload.Replied);
        Assert.Equal("vocabulary.single", payload.Intent);
        Assert.Contains("❓ Did you mean:", sender.LastText, StringComparison.Ordinal);
        Assert.DoesNotContain("❓ Word", sender.LastText, StringComparison.Ordinal);
        Assert.Contains("Did you mean:", sender.LastText, StringComparison.Ordinal);
        Assert.Contains("not recognized", sender.LastText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Did you mean", sender.LastText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("smile", sender.LastText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Processed.", sender.LastText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Webhook_ShouldShowDeckSource_WhenWordAlreadyExistsInDictionary()
    {
        var orchestrator = new FakeConversationOrchestrator
        {
            NextResult = BuildVocabularyFoundInDeckResult()
        };
        var sender = new FakeTelegramBotSender();

        var sut = CreateSut(
            orchestrator,
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            assistantSessionService: null,
            localeStateService: new FakeUserLocaleStateService
            {
                StoredLocale = LocalizationConstants.EnglishLocale,
                NextLocale = LocalizationConstants.EnglishLocale
            },
            navigationStateService: new FakeNavigationStateService { CurrentSection = NavigationSections.Vocabulary },
            vocabularyCardRepository: null,
            navigationPresenter: null,
            new FakeTelegramFormatter("fast\n\n(adj) quick"),
            sender,
            new TelegramOptions { Enabled = true });

        var response = await sut.Webhook(
            BuildTextUpdate(1001, 2002, "fast", null, languageCode: "en"),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);

        Assert.True(payload.Replied);
        Assert.Equal("vocabulary.single", payload.Intent);
        Assert.Contains("already exists in dictionary", sender.LastText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("wm-adjectives-ua-en.xlsx", sender.LastText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Webhook_ShouldInsertSeparatorBeforeMultiFoundInDeckSection()
    {
        var orchestrator = new FakeConversationOrchestrator
        {
            NextResult = BuildVocabularyBatchFoundInDeckResult()
        };
        var sender = new FakeTelegramBotSender();

        var sut = CreateSut(
            orchestrator,
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            assistantSessionService: null,
            localeStateService: new FakeUserLocaleStateService
            {
                StoredLocale = LocalizationConstants.EnglishLocale,
                NextLocale = LocalizationConstants.EnglishLocale
            },
            navigationStateService: new FakeNavigationStateService { CurrentSection = NavigationSections.Vocabulary },
            vocabularyCardRepository: null,
            navigationPresenter: null,
            new FakeTelegramFormatter("batch body"),
            sender,
            new TelegramOptions { Enabled = true });

        var response = await sut.Webhook(
            BuildTextUpdate(1001, 2002, "cancel celebrate concern", null, languageCode: "en"),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);

        Assert.True(payload.Replied);
        Assert.Equal("vocabulary.batch", payload.Intent);
        var normalized = sender.LastText.Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.Contains(
            "--------------------\nThese words already exist in dictionary:",
            normalized,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "\n\n--------------------\nThese words already exist in dictionary:",
            normalized,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Webhook_ShouldRenderPreviewWarnings_WithWarningMarkerStyle()
    {
        var orchestrator = new FakeConversationOrchestrator
        {
            NextResult = BuildVocabularyPreviewWarningResult()
        };
        var sender = new FakeTelegramBotSender();

        var sut = CreateSut(
            orchestrator,
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            assistantSessionService: null,
            localeStateService: new FakeUserLocaleStateService
            {
                StoredLocale = LocalizationConstants.EnglishLocale,
                NextLocale = LocalizationConstants.EnglishLocale
            },
            navigationStateService: new FakeNavigationStateService { CurrentSection = NavigationSections.Vocabulary },
            vocabularyCardRepository: null,
            navigationPresenter: null,
            new FakeTelegramFormatter("rest\n\n(v) rest"),
            sender,
            new TelegramOptions { Enabled = true });

        var response = await sut.Webhook(
            BuildTextUpdate(1001, 2002, "rest", null, languageCode: "en"),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);

        Assert.True(payload.Replied);
        Assert.Equal("vocabulary.single", payload.Intent);
        Assert.Contains("⚠️", sender.LastText, StringComparison.Ordinal);
        Assert.DoesNotContain("warning:", sender.LastText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Webhook_LanguageChangeInSettings_ShouldSendMainReplyKeyboardInNewLocale()
    {
        var localeState = new FakeUserLocaleStateService
        {
            StoredLocale = LocalizationConstants.UkrainianLocale,
            NextLocale = LocalizationConstants.UkrainianLocale
        };
        var sender = new FakeTelegramBotSender();
        var navigationState = new FakeNavigationStateService { CurrentSection = NavigationSections.Settings };
        var presenter = new TelegramNavigationPresenter(new LocalizationService());

        var sut = CreateSut(
            new FakeConversationOrchestrator(),
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            assistantSessionService: null,
            localeStateService: localeState,
            navigationStateService: navigationState,
            vocabularyCardRepository: null,
            navigationPresenter: presenter,
            new FakeTelegramFormatter("ignored"),
            sender,
            new TelegramOptions { Enabled = true });

        var response = await sut.Webhook(
            BuildCallbackUpdate(1001, 2002, CallbackDataConstants.Lang.English, null),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);

        Assert.True(payload.Replied);
        Assert.Equal("settings.language.changed", payload.Intent);
        Assert.Equal(2, sender.Calls);

        var refreshMessage = sender.SentMessages.Last();
        var refreshKeyboard = Assert.IsType<TelegramReplyKeyboardMarkup>(refreshMessage.Options?.ReplyMarkup);
        var labels = refreshKeyboard.Keyboard.SelectMany(row => row).Select(button => button.Text).ToList();

        Assert.Contains(labels, x => x.Contains("Vocabulary", StringComparison.Ordinal));
        Assert.DoesNotContain(labels, x => x.Contains("Словник", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Webhook_MessageFrom_WithNullLanguageCode_ResolvesToDefaultLocale()
    {
        var localeState = new FakeUserLocaleStateService
        {
            StoredLocale = null,
            NextLocale = LocalizationConstants.EnglishLocale,
            StoreLocaleOnEnsureWhenMissing = true
        };
        var sender = new FakeTelegramBotSender();

        var sut = CreateSut(
            new FakeConversationOrchestrator(),
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            assistantSessionService: null,
            localeStateService: localeState,
            navigationStateService: new FakeNavigationStateService { CurrentSection = NavigationSections.Vocabulary },
            vocabularyCardRepository: null,
            navigationPresenter: null,
            new FakeTelegramFormatter("ignored"),
            sender,
            new TelegramOptions { Enabled = true });

        var response = await sut.Webhook(
            BuildTextUpdate(1001, 2002, "prepare", null, languageCode: null),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);

        Assert.True(payload.Processed);
        Assert.True(payload.Replied);
        Assert.Null(localeState.LastEnsureTelegramLanguageCode);
        Assert.Equal(LocalizationConstants.EnglishLocale, localeState.StoredLocale);
    }

    private static TelegramController CreateSut(
        FakeConversationOrchestrator orchestrator,
        FakeConversationScopeAccessor scopeAccessor,
        FakeVocabularyStorageModeProvider storageModeProvider,
        FakeVocabularyStoragePreferenceService storagePreferenceService,
        FakeTelegramFormatter formatter,
        FakeTelegramBotSender sender,
        TelegramOptions options,
        FakeTelegramProcessedUpdateRepository? processedUpdates = null,
        FakeGraphAuthService? graphAuthService = null,
        FakeVocabularySyncProcessor? vocabularySyncProcessor = null,
        FakeVocabularyIndexService? vocabularyIndexService = null,
        FakeVocabularyDeckService? vocabularyDeckService = null,
        FakeVocabularyDiscoveryService? vocabularyDiscoveryService = null,
        FakeTelegramImportSourceReader? importSourceReader = null,
        FakeUserMemoryRepository? userMemoryRepository = null,
        FakeUnitOfWork? unitOfWork = null)
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
            processedUpdates,
            graphAuthService: graphAuthService,
            vocabularySyncProcessor: vocabularySyncProcessor,
            vocabularyIndexService: vocabularyIndexService,
            vocabularyDeckService: vocabularyDeckService,
            vocabularyDiscoveryService: vocabularyDiscoveryService,
            importSourceReader: importSourceReader,
            userMemoryRepository: userMemoryRepository,
            unitOfWork: unitOfWork);
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
        FakeTelegramProcessedUpdateRepository? processedUpdates = null,
        FakeVocabularyPersistenceService? vocabularyPersistenceService = null,
        FakeGraphAuthService? graphAuthService = null,
        FakeVocabularySyncProcessor? vocabularySyncProcessor = null,
        FakeVocabularyIndexService? vocabularyIndexService = null,
        FakeVocabularyDeckService? vocabularyDeckService = null,
        FakeVocabularyDiscoveryService? vocabularyDiscoveryService = null,
        FakeTelegramImportSourceReader? importSourceReader = null,
        FakeUserMemoryRepository? userMemoryRepository = null,
        FakeUnitOfWork? unitOfWork = null)
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
            processedUpdates,
            vocabularyPersistenceService,
            graphAuthService: graphAuthService,
            vocabularySyncProcessor: vocabularySyncProcessor,
            vocabularyIndexService: vocabularyIndexService,
            vocabularyDeckService: vocabularyDeckService,
            vocabularyDiscoveryService: vocabularyDiscoveryService,
            importSourceReader: importSourceReader,
            userMemoryRepository: userMemoryRepository,
            unitOfWork: unitOfWork);
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
        ITelegramNavigationPresenter? navigationPresenter,
        FakeTelegramFormatter formatter,
        FakeTelegramBotSender sender,
        TelegramOptions options,
        FakeTelegramProcessedUpdateRepository? processedUpdates = null,
        FakeVocabularyPersistenceService? vocabularyPersistenceService = null,
        FakeGraphAuthService? graphAuthService = null,
        FakeVocabularySyncProcessor? vocabularySyncProcessor = null,
        FakeVocabularyIndexService? vocabularyIndexService = null,
        FakeVocabularyDeckService? vocabularyDeckService = null,
        FakeVocabularyDiscoveryService? vocabularyDiscoveryService = null,
        FakeTelegramImportSourceReader? importSourceReader = null,
        FakeUserMemoryRepository? userMemoryRepository = null,
        FakeUnitOfWork? unitOfWork = null,
        FakeFoodTrackingService? foodTrackingService = null)
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
            new FakeVocabularySaveModePreferenceService(),
            vocabularyPersistenceService ?? new FakeVocabularyPersistenceService(),
            vocabularySyncProcessor ?? new FakeVocabularySyncProcessor(),
            vocabularyIndexService ?? new FakeVocabularyIndexService(),
            vocabularyDeckService ?? new FakeVocabularyDeckService(),
            new VocabularyReplyParser(),
            vocabularyDiscoveryService ?? new FakeVocabularyDiscoveryService(),
            importSourceReader ?? new FakeTelegramImportSourceReader(),
            new VocabularyDeckOptions(),
            graphAuthService ?? new FakeGraphAuthService(),
            navigationPresenter ?? new FakeTelegramNavigationPresenter(),
            formatter,
            sender,
            processedUpdates ?? new FakeTelegramProcessedUpdateRepository(),
            Options.Create(options),
            NullLogger<TelegramController>.Instance,
            userMemoryRepository,
            unitOfWork,
            notionOptions: null,
            notionFoodOptions: null,
            notionSyncWorkerOptions: null,
            foodSyncWorkerOptions: null,
            foodTrackingService,
            new TelegramPendingStateStore());
    }

    private static TelegramWebhookUpdateRequest BuildTextUpdate(
        long chatId,
        long userId,
        string text,
        int? messageThreadId,
        string? languageCode = "en",
        long updateId = 1)
    {
        return new TelegramWebhookUpdateRequest(
            UpdateId: updateId,
            Message: new TelegramIncomingMessage(
                MessageId: 10,
                From: new TelegramUserInfo(userId, false, languageCode, "mike", "Mike", null),
                Chat: new TelegramChatInfo(chatId, "private", "mike", null),
                Text: text,
                Caption: null,
                MessageThreadId: messageThreadId),
            EditedMessage: null,
            CallbackQuery: null);
    }

    private static TelegramWebhookUpdateRequest BuildDocumentUpdate(
        long chatId,
        long userId,
        string documentFileId,
        string? documentFileName,
        string? documentMimeType,
        int? messageThreadId,
        string? languageCode = "en",
        long updateId = 1)
    {
        return new TelegramWebhookUpdateRequest(
            UpdateId: updateId,
            Message: new TelegramIncomingMessage(
                MessageId: 10,
                From: new TelegramUserInfo(userId, false, languageCode, "mike", "Mike", null),
                Chat: new TelegramChatInfo(chatId, "private", "mike", null),
                Text: null,
                Caption: null,
                MessageThreadId: messageThreadId,
                Document: new TelegramIncomingDocument(documentFileId, "uniq", documentFileName, documentMimeType, 2048)),
            EditedMessage: null,
            CallbackQuery: null);
    }

    private static TelegramWebhookUpdateRequest BuildPhotoUpdate(
        long chatId,
        long userId,
        string photoFileId,
        int? messageThreadId,
        string? languageCode = "en",
        long updateId = 1)
    {
        return new TelegramWebhookUpdateRequest(
            UpdateId: updateId,
            Message: new TelegramIncomingMessage(
                MessageId: 10,
                From: new TelegramUserInfo(userId, false, languageCode, "mike", "Mike", null),
                Chat: new TelegramChatInfo(chatId, "private", "mike", null),
                Text: null,
                Caption: null,
                MessageThreadId: messageThreadId,
                Document: null,
                Photo: [new TelegramIncomingPhotoSize(photoFileId, "uniq-photo", 800, 600, 1024)]),
            EditedMessage: null,
            CallbackQuery: null);
    }

    private static ConversationAgentResult BuildVocabularySingleResult(string inputWord = "smile")
    {
        var item = new ConversationAgentItemResult(
            Input: inputWord,
            Lookup: new VocabularyLookupResult(inputWord, []),
            AssistantCompletion: new AssistantCompletionResult(
                $"{inputWord}\n\n(v) усміхатися\n\nShe smiled after fixing the bug.",
                "test-model",
                Usage: null),
            AppendPreview: new VocabularyAppendPreviewResult(
                Status: VocabularyAppendPreviewStatus.ReadyToAppend,
                Word: inputWord,
                TargetDeckFileName: "wm-verbs-us-en.xlsx",
                TargetDeckPath: "/tmp/wm-verbs-us-en.xlsx",
                DuplicateMatches: null,
                Message: null));

        return new ConversationAgentResult(
            AgentName: "vocabulary-agent",
            Intent: "vocabulary.single",
            IsBatch: false,
            Items: [item]);
    }

    private static TelegramWebhookUpdateRequest BuildCallbackUpdate(
        long chatId,
        long userId,
        string? callbackData,
        int? messageThreadId,
        long updateId = 2,
        string? languageCode = "en")
    {
        return new TelegramWebhookUpdateRequest(
            UpdateId: updateId,
            Message: null,
            EditedMessage: null,
            CallbackQuery: new TelegramCallbackQuery(
                Id: "cb-1",
                From: new TelegramUserInfo(userId, false, languageCode, "mike", "Mike", null),
                Message: new TelegramIncomingMessage(
                    MessageId: 99,
                    From: new TelegramUserInfo(userId, false, languageCode, "mike", "Mike", null),
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

        public string LastLocale { get; private set; } = "en";

        public string? LastUserId { get; private set; }

        public string? LastConversationId { get; private set; }

        public Queue<ConversationAgentResult> NextResults { get; } = new();

        public ConversationAgentResult NextResult { get; set; } = new(
            AgentName: "vocabulary-agent",
            Intent: "vocabulary.single",
            IsBatch: false,
            Items: []);

        public Action<string, string, string?, string?>? OnProcess { get; set; }

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
            string locale,
            CancellationToken cancellationToken)
            => ProcessAsync(input, channel, locale, null, null, cancellationToken);

        public Task<ConversationAgentResult> ProcessAsync(
            string input,
            string channel,
            string locale,
            string? userId,
            string? conversationId,
            CancellationToken cancellationToken = default)
        {
            LastLocale = locale;
            return ProcessAsync(input, channel, userId, conversationId, cancellationToken);
        }

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
            OnProcess?.Invoke(input, channel, userId, conversationId);
            if (NextResults.Count > 0)
            {
                return Task.FromResult(NextResults.Dequeue());
            }

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

    private sealed class FakeVocabularySaveModePreferenceService : IVocabularySaveModePreferenceService
    {
        public IReadOnlyList<string> SupportedModes { get; } = ["auto", "ask", "off"];

        public VocabularySaveMode CurrentMode { get; set; } = VocabularySaveMode.Ask;

        public bool TryParse(string? value, out VocabularySaveMode mode)
        {
            mode = VocabularySaveMode.Ask;
            return value?.Trim().ToLowerInvariant() switch
            {
                "auto" => (mode = VocabularySaveMode.Auto) == VocabularySaveMode.Auto,
                "ask" => (mode = VocabularySaveMode.Ask) == VocabularySaveMode.Ask,
                "off" => (mode = VocabularySaveMode.Off) == VocabularySaveMode.Off,
                _ => false
            };
        }

        public string ToText(VocabularySaveMode mode)
            => mode switch
            {
                VocabularySaveMode.Auto => "auto",
                VocabularySaveMode.Off => "off",
                _ => "ask"
            };

        public Task<VocabularySaveMode> GetModeAsync(ConversationScope scope, CancellationToken cancellationToken = default)
            => Task.FromResult(CurrentMode);

        public Task<VocabularySaveMode> SetModeAsync(ConversationScope scope, VocabularySaveMode mode, CancellationToken cancellationToken = default)
        {
            CurrentMode = mode;
            return Task.FromResult(mode);
        }
    }

    private sealed class FakeVocabularyPersistenceService : IVocabularyPersistenceService
    {
        public int Calls { get; private set; }

        public string? LastRequestedWord { get; private set; }

        public string? LastAssistantReply { get; private set; }

        public string? LastForcedDeckFileName { get; private set; }

        public string? LastOverridePartOfSpeech { get; private set; }

        public VocabularyAppendResult NextResult { get; set; } = new(
            VocabularyAppendStatus.Added,
            Entry: new VocabularyDeckEntry("wm-verbs-us-en.xlsx", "/tmp/wm-verbs-us-en.xlsx", 42, "smile", "(v) усміхатися", "She smiled."));

        public Task<VocabularyAppendResult> AppendFromAssistantReplyAsync(
            string requestedWord,
            string assistantReply,
            string? forcedDeckFileName = null,
            string? overridePartOfSpeech = null,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            LastRequestedWord = requestedWord;
            LastAssistantReply = assistantReply;
            LastForcedDeckFileName = forcedDeckFileName;
            LastOverridePartOfSpeech = overridePartOfSpeech;

            return Task.FromResult(NextResult);
        }
    }

    private sealed class FakeGraphAuthService : IGraphAuthService
    {
        public GraphAuthStatus Status { get; set; } = new(true, false, "Not authenticated.");

        public Task<GraphAuthStatus> GetStatusAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Status);

        public Task<GraphLoginResult> LoginAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new GraphLoginResult(Status.IsAuthenticated, Status.Message));

        public Task<GraphLoginResult> LoginAsync(Func<GraphDeviceCodePrompt, CancellationToken, Task> onDeviceCodeReceived, CancellationToken cancellationToken = default)
            => Task.FromResult(new GraphLoginResult(Status.IsAuthenticated, Status.Message));

        public Task<GraphDeviceLoginStartResult> StartLoginAsync(CancellationToken cancellationToken = default)
        {
            var challenge = new GraphDeviceLoginChallenge(
                "device",
                "code",
                "https://www.microsoft.com/link",
                ExpiresInSeconds: 900,
                IntervalSeconds: 5,
                ExpiresAtUtc: DateTimeOffset.UtcNow.AddMinutes(15));

            return Task.FromResult(new GraphDeviceLoginStartResult(true, "started", challenge));
        }

        public Task<GraphLoginResult> CompleteLoginAsync(GraphDeviceLoginChallenge challenge, CancellationToken cancellationToken = default)
        {
            Status = Status with { IsAuthenticated = true, Message = "Authenticated." };
            return Task.FromResult(new GraphLoginResult(true, "ok"));
        }

        public Task LogoutAsync(CancellationToken cancellationToken = default)
        {
            Status = Status with { IsAuthenticated = false, Message = "Not authenticated." };
            return Task.CompletedTask;
        }

        public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);
    }

    private static ConversationAgentResult BuildVocabularyUnrecognizedResult()
    {
        var item = new ConversationAgentItemResult(
            Input: "smle",
            Lookup: new VocabularyLookupResult("smle", []),
            AssistantCompletion: null,
            AppendPreview: null)
        {
            IsWordUnrecognized = true,
            WordSuggestions = ["smile", "smiley"]
        };

        return new ConversationAgentResult(
            AgentName: "vocabulary-agent",
            Intent: "vocabulary.single",
            IsBatch: false,
            Items: [item]);
    }

    private static ConversationAgentResult BuildVocabularyFoundInDeckResult()
    {
        var item = new ConversationAgentItemResult(
            Input: "fast",
            Lookup: new VocabularyLookupResult(
                "fast",
                [
                    new VocabularyDeckEntry(
                        DeckFileName: "wm-adjectives-ua-en.xlsx",
                        DeckPath: "/apps/Flashcards Deluxe/wm-adjectives-ua-en.xlsx",
                        RowNumber: 145,
                        Word: "fast",
                        Meaning: "(adj) quick",
                        Examples: "The API is fast.")
                ]));

        return new ConversationAgentResult(
            AgentName: "vocabulary-agent",
            Intent: "vocabulary.single",
            IsBatch: false,
            Items: [item]);
    }

    private static ConversationAgentResult BuildVocabularyBatchFoundInDeckResult()
    {
        var items = new List<ConversationAgentItemResult>
        {
            new(
                Input: "cancel",
                Lookup: new VocabularyLookupResult(
                    "cancel",
                    [
                        new VocabularyDeckEntry(
                            DeckFileName: "wm-verbs-us-en.xlsx",
                            DeckPath: "/apps/Flashcards Deluxe/wm-verbs-us-en.xlsx",
                            RowNumber: 624,
                            Word: "cancel",
                            Meaning: "(v) cancel",
                            Examples: string.Empty)
                    ])),
            new(
                Input: "celebrate",
                Lookup: new VocabularyLookupResult(
                    "celebrate",
                    [
                        new VocabularyDeckEntry(
                            DeckFileName: "wm-verbs-us-en.xlsx",
                            DeckPath: "/apps/Flashcards Deluxe/wm-verbs-us-en.xlsx",
                            RowNumber: 625,
                            Word: "celebrate",
                            Meaning: "(v) celebrate",
                            Examples: string.Empty)
                    ])),
            new(
                Input: "concern",
                Lookup: new VocabularyLookupResult(
                    "concern",
                    [
                        new VocabularyDeckEntry(
                            DeckFileName: "wm-nouns-ua-en.xlsx",
                            DeckPath: "/apps/Flashcards Deluxe/wm-nouns-ua-en.xlsx",
                            RowNumber: 836,
                            Word: "concern",
                            Meaning: "(n) concern",
                            Examples: string.Empty)
                    ]))
        };

        return new ConversationAgentResult(
            AgentName: "vocabulary-agent",
            Intent: "vocabulary.batch",
            IsBatch: true,
            Items: items);
    }

    private static ConversationAgentResult BuildVocabularyBatchSavableResult()
    {
        var items = new List<ConversationAgentItemResult>
        {
            new(
                Input: "awkward",
                Lookup: new VocabularyLookupResult("awkward", []),
                AssistantCompletion: new AssistantCompletionResult(
                    "awkward\n\n(adj) незручний\n\nThe awkward error message confused users",
                    "test-model",
                    null),
                AppendPreview: new VocabularyAppendPreviewResult(
                    Status: VocabularyAppendPreviewStatus.ReadyToAppend,
                    Word: "awkward",
                    TargetDeckFileName: "wm-adjectives-ua-en.xlsx",
                    TargetDeckPath: "/apps/Flashcards Deluxe/wm-adjectives-ua-en.xlsx",
                    DuplicateMatches: null,
                    Message: string.Empty)),
            new(
                Input: "exact",
                Lookup: new VocabularyLookupResult("exact", []),
                AssistantCompletion: new AssistantCompletionResult(
                    "exact\n\n(adj) точний\n\nPlease provide the exact specifications for the software",
                    "test-model",
                    null),
                AppendPreview: new VocabularyAppendPreviewResult(
                    Status: VocabularyAppendPreviewStatus.ReadyToAppend,
                    Word: "exact",
                    TargetDeckFileName: "wm-adjectives-ua-en.xlsx",
                    TargetDeckPath: "/apps/Flashcards Deluxe/wm-adjectives-ua-en.xlsx",
                    DuplicateMatches: null,
                    Message: string.Empty))
        };

        return new ConversationAgentResult(
            AgentName: "vocabulary-agent",
            Intent: "vocabulary.batch",
            IsBatch: true,
            Items: items);
    }

    private static ConversationAgentResult BuildVocabularyPreviewWarningResult()
    {
        var item = new ConversationAgentItemResult(
            Input: "rest",
            Lookup: new VocabularyLookupResult("rest", []),
            AssistantCompletion: new AssistantCompletionResult(
                "rest\n\n(v) rest",
                "test-model",
                Usage: null),
            AppendPreview: new VocabularyAppendPreviewResult(
                Status: VocabularyAppendPreviewStatus.Error,
                Word: "rest",
                TargetDeckFileName: "wm-verbs-us-en.xlsx",
                TargetDeckPath: "/apps/Flashcards Deluxe/wm-verbs-us-en.xlsx",
                DuplicateMatches: null,
                Message: "Graph authentication is required. Run /graph login first."));

        return new ConversationAgentResult(
            AgentName: "vocabulary-agent",
            Intent: "vocabulary.single",
            IsBatch: false,
            Items: [item]);
    }

    private sealed class FakeVocabularySyncProcessor : IVocabularySyncProcessor
    {
        public int ProcessCalls { get; private set; }

        public VocabularySyncRunSummary NextSummary { get; set; } = new(
            Requested: 0,
            Processed: 0,
            Completed: 0,
            Requeued: 0,
            Failed: 0,
            PendingAfterRun: 0);

        public Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(NextSummary.PendingAfterRun);

        public Task<VocabularySyncRunSummary> ProcessPendingAsync(int take, CancellationToken cancellationToken = default)
        {
            ProcessCalls++;
            return Task.FromResult(NextSummary);
        }

        public Task<IReadOnlyList<VocabularySyncFailedJob>> GetFailedJobsAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<VocabularySyncFailedJob>>([]);

        public Task<int> RequeueFailedAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }

    private sealed class FakeVocabularyIndexService : IVocabularyIndexService
    {
        public int RebuildCalls { get; private set; }
        public int ClearCalls { get; private set; }

        public IReadOnlyList<VocabularyDeckEntry> LastEntries { get; private set; } = [];

        public VocabularyStorageMode LastMode { get; private set; } = VocabularyStorageMode.Local;

        public int RebuildResult { get; set; }
        public int ClearResult { get; set; }

        public Task<VocabularyLookupResult> FindByInputAsync(string input, CancellationToken cancellationToken = default)
            => Task.FromResult(new VocabularyLookupResult(input, []));

        public Task<IReadOnlyDictionary<string, VocabularyLookupResult>> FindByInputsAsync(
            IReadOnlyList<string> inputs,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<string, VocabularyLookupResult>>(
                new Dictionary<string, VocabularyLookupResult>(StringComparer.OrdinalIgnoreCase));

        public Task IndexLookupResultAsync(
            VocabularyLookupResult lookup,
            VocabularyStorageMode storageMode,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task HandleAppendResultAsync(
            string requestedWord,
            string assistantReply,
            string? targetDeckFileName,
            string? overridePartOfSpeech,
            VocabularyAppendResult appendResult,
            VocabularyStorageMode storageMode,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<int> ClearAsync(CancellationToken cancellationToken = default)
        {
            ClearCalls++;
            return Task.FromResult(ClearResult);
        }

        public Task<int> RebuildAsync(
            IReadOnlyList<VocabularyDeckEntry> entries,
            VocabularyStorageMode storageMode,
            CancellationToken cancellationToken = default)
        {
            RebuildCalls++;
            LastEntries = entries;
            LastMode = storageMode;
            return Task.FromResult(RebuildResult);
        }
    }

    private sealed class FakeVocabularyDeckService : IVocabularyDeckService
    {
        public IReadOnlyList<VocabularyDeckEntry> AllEntries { get; set; } = [];
        public IReadOnlyList<VocabularyDeckFile> WritableDeckFiles { get; set; } = [];

        public Task<VocabularyLookupResult> FindInWritableDecksAsync(string word, CancellationToken cancellationToken = default)
            => Task.FromResult(new VocabularyLookupResult(word, []));

        public Task<IReadOnlyList<VocabularyDeckFile>> GetWritableDeckFilesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(WritableDeckFiles);

        public Task<VocabularyAppendPreviewResult> PreviewAppendFromAssistantReplyAsync(
            string requestedWord,
            string assistantReply,
            string? forcedDeckFileName = null,
            string? overridePartOfSpeech = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new VocabularyAppendPreviewResult(VocabularyAppendPreviewStatus.NoMatchingDeck, requestedWord));

        public Task<VocabularyAppendResult> AppendFromAssistantReplyAsync(
            string requestedWord,
            string assistantReply,
            string? forcedDeckFileName = null,
            string? overridePartOfSpeech = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new VocabularyAppendResult(VocabularyAppendStatus.Error, Message: "Not supported in fake"));

        public Task<IReadOnlyList<VocabularyDeckEntry>> GetAllEntriesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(AllEntries);
    }

    private sealed class FakeVocabularyDiscoveryService : IVocabularyDiscoveryService
    {
        public int Calls { get; private set; }

        public string? LastSourceInput { get; private set; }

        public VocabularyDiscoveryResult NextResult { get; set; } = new(
            VocabularyDiscoveryStatus.Success,
            [
                new VocabularyDiscoveryCandidate("architecture", "n", 3),
                new VocabularyDiscoveryCandidate("deploy", "v", 2),
                new VocabularyDiscoveryCandidate("scalable", "adj", 1)
            ],
            "ok",
            SourceWasUrl: true);

        public Task<VocabularyDiscoveryResult> DiscoverAsync(
            string sourceInput,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            LastSourceInput = sourceInput;
            return Task.FromResult(NextResult);
        }
    }

    private sealed class FakeTelegramImportSourceReader : ITelegramImportSourceReader
    {
        public int Calls { get; private set; }

        public TelegramImportSourceType? LastSourceType { get; private set; }

        public TelegramImportInbound LastInbound { get; private set; } = new(string.Empty, null, null, null, null);

        public TelegramImportSourceReadResult NextResult { get; set; }
            = new(TelegramImportSourceReadStatus.Success);

        public Task<TelegramImportSourceReadResult> ReadTextAsync(
            TelegramImportInbound inbound,
            TelegramImportSourceType sourceType,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            LastInbound = inbound;
            LastSourceType = sourceType;
            if (NextResult.Status == TelegramImportSourceReadStatus.Success)
            {
                var text = string.IsNullOrWhiteSpace(NextResult.Text)
                    ? inbound.Text
                    : NextResult.Text;
                return Task.FromResult(new TelegramImportSourceReadResult(TelegramImportSourceReadStatus.Success, text));
            }

            return Task.FromResult(NextResult);
        }

        public TelegramFoodIdentificationResult NextFoodResult { get; set; }
            = new(true, "Test Meal", 500);

        public Task<TelegramFoodIdentificationResult> IdentifyFoodAsync(
            string photoFileId,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(NextFoodResult);
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
        public string? StoredLocale { get; set; } = "en";
        public string? LastEnsureTelegramLanguageCode { get; private set; }
        public bool StoreLocaleOnEnsureWhenMissing { get; set; }

        public Task<string?> GetStoredLocaleAsync(
            string channel,
            string userId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(StoredLocale);

        public Task<string> SetLocaleAsync(
            string channel,
            string userId,
            string locale,
            bool selectedManually,
            CancellationToken cancellationToken = default)
        {
            StoredLocale = locale;
            NextLocale = locale;
            return Task.FromResult(locale);
        }

        public Task<UserLocaleStateResult> EnsureLocaleAsync(
            string channel,
            string userId,
            string? telegramLanguageCode,
            string? incomingText,
            CancellationToken cancellationToken = default)
        {
            LastEnsureTelegramLanguageCode = telegramLanguageCode;

            if (StoreLocaleOnEnsureWhenMissing && string.IsNullOrWhiteSpace(StoredLocale))
            {
                StoredLocale = string.IsNullOrWhiteSpace(telegramLanguageCode)
                    ? LocalizationConstants.EnglishLocale
                    : LocalizationConstants.NormalizeLocaleCode(telegramLanguageCode);
                NextLocale = StoredLocale;
            }

            return Task.FromResult(new UserLocaleStateResult(NextLocale, IsInitialized: false, IsSwitched: false));
        }
    }

    private sealed class FakeNavigationStateService : INavigationStateService
    {
        public string CurrentSection { get; set; } = "main";
        public bool ThrowOnSetCurrentSection { get; set; }

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
            if (ThrowOnSetCurrentSection)
            {
                throw new InvalidOperationException("Simulated SetCurrentSection failure.");
            }

            CurrentSection = section;
            return Task.FromResult(section);
        }
    }

    private sealed class FakeVocabularyCardRepository : IVocabularyCardRepository
    {
        public int CountAllResult { get; set; }
        public IReadOnlyList<VocabularyDeckStat> DeckStatsResult { get; set; } = [];
        public IReadOnlyList<VocabularyPartOfSpeechStat> PartOfSpeechStatsResult { get; set; } = [];
        public bool ThrowOnCountAll { get; set; }
        public bool ThrowOnGetDeckStats { get; set; }
        public bool ThrowOnGetPartOfSpeechStats { get; set; }

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
        {
            if (ThrowOnCountAll)
            {
                throw new InvalidOperationException("Simulated count failure.");
            }

            return Task.FromResult(CountAllResult);
        }

        public Task<IReadOnlyList<VocabularyCard>> GetRecentAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<VocabularyCard>>([]);

        public Task<IReadOnlyList<VocabularyDeckStat>> GetDeckStatsAsync(CancellationToken cancellationToken = default)
        {
            if (ThrowOnGetDeckStats)
            {
                throw new InvalidOperationException("Simulated deck stats failure.");
            }

            return Task.FromResult(DeckStatsResult);
        }

        public Task<IReadOnlyList<VocabularyPartOfSpeechStat>> GetPartOfSpeechStatsAsync(CancellationToken cancellationToken = default)
        {
            if (ThrowOnGetPartOfSpeechStats)
            {
                throw new InvalidOperationException("Simulated POS stats failure.");
            }

            return Task.FromResult(PartOfSpeechStatsResult);
        }

        public Task<int> DeleteAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }

    private sealed class FakeUserMemoryRepository : IUserMemoryRepository
    {
        private readonly Dictionary<(string Key, string Channel, string UserId), UserMemoryEntry> _entries = new();

        public Task<UserMemoryEntry?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
        {
            var result = _entries.Values.FirstOrDefault(x => x.Key.Equals(key, StringComparison.Ordinal));
            return Task.FromResult(result);
        }

        public Task<UserMemoryEntry?> GetByKeyAsync(
            string key,
            string channel,
            string userId,
            CancellationToken cancellationToken = default)
        {
            _entries.TryGetValue((key, channel, userId), out var entry);
            return Task.FromResult(entry);
        }

        public Task<IReadOnlyList<UserMemoryEntry>> GetActiveAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<UserMemoryEntry>>(
                _entries.Values
                    .Where(x => x.IsActive)
                    .OrderByDescending(x => x.LastSeenAtUtc)
                    .Take(take)
                    .ToList());

        public Task<IReadOnlyList<UserMemoryEntry>> GetActiveAsync(
            int take,
            string channel,
            string userId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<UserMemoryEntry>>(
                _entries.Values
                    .Where(x => x.IsActive && x.Channel.Equals(channel, StringComparison.Ordinal) && x.UserId.Equals(userId, StringComparison.Ordinal))
                    .OrderByDescending(x => x.LastSeenAtUtc)
                    .Take(take)
                    .ToList());

        public Task AddAsync(UserMemoryEntry entry, CancellationToken cancellationToken = default)
        {
            _entries[(entry.Key, entry.Channel, entry.UserId)] = entry;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveChangesCalls { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCalls++;
            return Task.FromResult(1);
        }

        public Task BeginTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task CommitTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RollbackTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void Dispose()
        {
        }
    }

    private sealed class FakeTelegramNavigationPresenter : ITelegramNavigationPresenter
    {
        public string LastMainReplyKeyboardLocale { get; private set; } = LocalizationConstants.EnglishLocale;

        public MainMenuLabels GetMainMenuLabels(string locale)
            => new("Chat", "Vocabulary", "Shopping", "Menu", "Settings");

        public string GetText(string key, string locale, params object[] args)
        {
            var value = key switch
            {
                "menu.main.title" => "What can I help you with?",
                "start.welcome" => "Welcome",
                "stub.wip" => "WIP",
                "menu.vocabulary.title" => "Vocabulary {0}",
                "vocab.add.prompt" => "Add word",
                "vocab.import.choose_source" => "Choose import source:",
                "vocab.import.source.photo" => "Photo",
                "vocab.import.source.file" => "File",
                "vocab.import.source.url" => "URL",
                "vocab.import.source.text" => "Text",
                "vocab.import.prompt.photo" => "Send photo",
                "vocab.import.prompt.file" => "Send file",
                "vocab.import.prompt.url" => "Send URL",
                "vocab.import.prompt.text" => "Send text",
                "vocab.import.invalid_expected_photo" => "Waiting for photo",
                "vocab.import.invalid_expected_file" => "Waiting for file",
                "vocab.import.invalid_expected_url" => "Waiting for URL",
                "vocab.import.invalid_expected_text" => "Waiting for text",
                "vocab.import.file_unsupported" => "Unsupported file type",
                "vocab.import.photo_no_text" => "No text on photo",
                "vocab.import.file_no_text" => "No text in file",
                "vocab.import.read_failed" => "Import failed: {0}",
                "vocab.url.prompt" => "Send URL",
                "vocab.url.invalid" => "Invalid source",
                "vocab.url.empty" => "No candidates",
                "vocab.url.suggestions_title" => "Suggested new words: {0}",
                "vocab.url.suggestions_group_n" => "Nouns",
                "vocab.url.suggestions_group_v" => "Verbs",
                "vocab.url.suggestions_group_adj" => "Adjectives",
                "vocab.url.suggestions_hint" => "Reply with numbers",
                "vocab.url.select_parse_failed" => "Could not parse selection",
                "vocab.url.selection_cancelled" => "Cancelled",
                "vocab.url.no_pending" => "No pending URL/text import request.",
                "vocab.url.select_all" => "Add all",
                "vocab.url.cancel" => "Cancel",
                "food.shop.add.prompt" => "What would you like to add to the shopping list?",
                "food.shop.added" => "Added \"{0}\"{1}{2} to your shopping list.",
                "food.shop.qty_suffix" => " × {0}",
                "food.shop.store_suffix" => " at {0}",
                "food.shop.delete.prompt.title" => "Choose item(s) to delete from shopping list ({0}):",
                "food.shop.delete.prompt.item" => "{0}) {1}",
                "food.shop.delete.prompt.hint" => "Send numbers or names.",
                "food.shop.delete.invalid" => "Please send item numbers or names to delete.",
                "food.shop.delete.no_match" => "No matching items found.",
                "food.shop.delete.done" => "Removed item(s): {0}.",
                "food.shop.delete.cancelled" => "Delete cancelled.",
                "shop.not_in_inventory" => "\"{0}\" was not found in inventory.",
                "shop.only_english" => "Product name must be in English. Shopping list accepts inventory items only.",
                "shop.add_inventory_first" => "Add this product to inventory first (in English), then add it to shopping list.",
                "shop.matched_inventory" => "Matched inventory item: \"{0}\".",
                "food.weekly.view.empty" => "No meals found. Add some meals to Notion Meal Plans first.",
                "food.weekly.view.title" => "Meal plans ({0} meals):",
                "food.weekly.view.line" => "🍽 [{0}] {1}{2}",
                "food.weekly.view.calories_suffix" => " — {0} kcal/serving",
                "food.weekly.logged" => "✅ Logged meal #{0} × {1} serving(s).",
                "food.weekly.log.not_found" => "⚠️ Meal with ID {0} not found.",
                "vocab.stats.empty" => "Stats empty",
                "vocab.stats.title" => "Vocabulary statistics",
                "vocab.stats.total" => "Total indexed words: {0}",
                "vocab.stats.summary" => "Decks: {0} | POS markers: {1}",
                "vocab.stats.by_marker" => "By part of speech:",
                "vocab.stats.top_decks" => "Top decks:",
                "vocab.stats.no_data" => "No data",
                "vocab.stats.marker_unknown" => "(unclassified)",
                "vocab.stats.item" => "• {0}: {1}",
                "vocab.stats.deck_item" => "{0}) {1} — {2}",
                "vocab.stats.and_more_decks" => "... and {0} more deck(s).",
                "settings.title" => "Settings",
                "settings.language" => "Language",
                "settings.save_mode" => "Save mode",
                "settings.storage_mode" => "Storage mode",
                "settings.onedrive" => "OneDrive / Graph",
                "settings.notion" => "Notion",
                "settings.notion_enabled" => "Enabled",
                "settings.notion_partial" => "Needs setup",
                "settings.notion_disabled" => "Disabled",
                "notion.title" => "<b>Notion</b>",
                "notion.vocabulary" => "Vocabulary export",
                "notion.food" => "Food sync",
                "notion.status_enabled" => "Enabled",
                "notion.status_disabled" => "Disabled",
                "notion.configured_yes" => "Configured",
                "notion.configured_no" => "Missing required keys",
                "notion.worker_enabled" => "Worker enabled",
                "notion.worker_disabled" => "Worker disabled",
                "notion.tip" => "Set Notion keys in Railway variables if you want syncing to run.",
                "savemode.title" => "Save mode: {0}",
                "savemode.changed" => "Changed to {0}",
                "storagemode.title" => "Storage mode: {0}",
                "storagemode.changed" => "Changed storage to {0}",
                "vocab.save.ask" => "Save \"{0}\" to \"{1}\"?",
                "vocab.save.saved" => "Saved to {0} (row {1}).",
                "vocab.save.duplicate" => "Duplicate",
                "vocab.save.skip" => "Save skipped",
                "vocab.save_failed" => "Save failed: {0}",
                "vocab.save_queued_waiting_auth" => "Queued until OneDrive sign-in",
                "vocab.graph_save_setup_required" => "To save words, open Settings -> OneDrive / Graph, sign in, and try again.",
                "vocab.save_batch_ask_hint" => "ℹ️ Batch ask hint",
                "vocab.save_batch_ask_question" => "Save all {0} new items from this batch?",
                "vocab.save_batch_done" => "Batch save finished: saved={0}, duplicates={1}, failed={2}.",
                "vocab.save_batch_skip" => "Batch save skipped",
                "vocab.save_mode_off_hint" => "Save mode off hint",
                "vocab.save_yes" => "Save",
                "vocab.save_no" => "Skip",
                "vocab.save_batch_yes" => "Save all",
                "vocab.save_batch_no" => "Skip all",
                "vocab.no_pending_save" => "No pending save",
                "vocab.word_unrecognized" => "Word \"{0}\" is not recognized.",
                "vocab.word_unrecognized_with_suggestions" => "Word \"{0}\" is not recognized. Did you mean: {1}?",
                "vocab.found_in_deck_single" => "Word already exists in dictionary: {0} (row {1}).",
                "vocab.found_in_deck_multi_title" => "These words already exist in dictionary:",
                "vocab.found_in_deck_multi_item" => "{0}: {1} (row {2})",
                "command.console_only_generic" => "The {0} command is console-only",
                "command.console_only_graph" => "The {0} command is console-only. Open Settings -> OneDrive / Graph.",
                "onedrive.login_switched_to_graph" => "Connected and switched to graph",
                "onedrive.title" => "OneDrive / Graph",
                "onedrive.status_connected" => "Status: connected",
                "onedrive.status_disconnected" => "Status: disconnected",
                "onedrive.error_not_authenticated" => "Authorization is required",
                "onedrive.error_expired" => "Session expired",
                "onedrive.error_not_configured" => "Graph is not configured",
                "onedrive.error_timed_out" => "Login timed out",
                "onedrive.error_declined" => "Login declined",
                "onedrive.still_not_signed_in" => "Still not signed in",
                "onedrive.sync_now" => "Sync pending saves",
                "onedrive.rebuild_index" => "Rebuild cache",
                "onedrive.rebuild_index_warning" => "⚠️ Rebuilding cache can take some time. Start now?",
                "onedrive.rebuild_index_start" => "Start rebuild",
                "onedrive.rebuild_index_started" => "Rebuilding cache started...",
                "onedrive.rebuild_index_suggest" => "Tip: cache appears empty. Rebuild it.",
                "onedrive.index_ready" => "Index is ready: {0} words.",
                "onedrive.sync_now_done" => "Sync complete: completed={0}, requeued={1}, failed={2}, pending={3}.",
                "onedrive.rebuild_index_done" => "Cache rebuilt from writable decks: scanned={0}, indexed={1}.",
                "onedrive.clear_cache" => "Clear cache",
                "onedrive.clear_cache_warning" => "⚠️ Clear cache? records={0}",
                "onedrive.clear_cache_start" => "Clear now",
                "onedrive.clear_cache_done" => "Cache cleared: {0}",
                "onedrive.clear_cache_hint" => "Run rebuild cache if needed.",
                "onedrive.operation_failed" => "Operation failed: {0}",
                "inventory.stats.title" => "Inventory stats",
                "inventory.stats.total_items" => "Total items: {0}",
                "inventory.stats.with_current" => "With current quantity: {0}",
                "inventory.stats.with_min" => "With min threshold: {0}",
                "inventory.stats.low_stock" => "Low stock items: {0}",
                "inventory.stats.total_current" => "Sum of current quantity: {0}",
                "inventory.adjust.prompt" => "Use format: <id> +/-<amount>",
                "inventory.adjust.hint" => "Example: 42 -1",
                "inventory.adjust.done" => "Updated {0}: {1}",
                "inventory.adjust.invalid" => "Invalid format",
                "inventory.adjust.not_found" => "Item not found: {0}",
                "onboarding.choose_language" => "Choose language",
                "language.current" => "Current: {0}",
                "language.changed" => "Changed: {0}",
                "onboarding.language_saved" => "Saved",
                _ => key
            };

            return args.Length == 0
                ? value
                : string.Format(value, args);
        }

        public string GetLanguageDisplayName(string locale) => locale;

        public TelegramReplyKeyboardMarkup BuildMainReplyKeyboard(string locale)
        {
            LastMainReplyKeyboardLocale = locale;
            return new([
                [new TelegramKeyboardButton("Chat"), new TelegramKeyboardButton("Vocabulary")],
                [new TelegramKeyboardButton("Shopping"), new TelegramKeyboardButton("Menu")],
                [new TelegramKeyboardButton("Settings")]
            ]);
        }

        public TelegramInlineKeyboardMarkup BuildVocabularyKeyboard(string locale)
            => new([[new TelegramInlineKeyboardButton("Add", "vocab:add")]]);

        public TelegramInlineKeyboardMarkup BuildFoodMenuKeyboard(string locale)
            => new([[new TelegramInlineKeyboardButton("Food", CallbackDataConstants.Food.Menu)]]);

        public TelegramInlineKeyboardMarkup BuildInventoryKeyboard(string locale)
            => new([[new TelegramInlineKeyboardButton("Inventory", CallbackDataConstants.Inventory.List)]]);

        public TelegramInlineKeyboardMarkup BuildShoppingKeyboard(string locale)
            => new([[new TelegramInlineKeyboardButton("Add", "shop:add")]]);

        public TelegramInlineKeyboardMarkup BuildWeeklyMenuKeyboard(string locale)
            => new([[new TelegramInlineKeyboardButton("View", "weekly:view")]]);

        public TelegramInlineKeyboardMarkup BuildOnboardingLanguageKeyboard(string locale)
            => new([[new TelegramInlineKeyboardButton("EN", "lang:en")]]);

        public TelegramInlineKeyboardMarkup BuildOnboardingSecondaryLanguageKeyboard(string locale)
            => new([[new TelegramInlineKeyboardButton("DE", "lang:de"), new TelegramInlineKeyboardButton("PL", "lang:pl")]]);

        public TelegramInlineKeyboardMarkup BuildSettingsKeyboard(string locale)
            => new([[new TelegramInlineKeyboardButton("Language", "settings:language")]]);

        public TelegramInlineKeyboardMarkup BuildSettingsLanguageKeyboard(string locale)
            => new([[new TelegramInlineKeyboardButton("EN", "lang:en")]]);

        public TelegramInlineKeyboardMarkup BuildSettingsSecondaryLanguageKeyboard(string locale)
            => new([[new TelegramInlineKeyboardButton("DE", "lang:de"), new TelegramInlineKeyboardButton("PL", "lang:pl")]]);

        public TelegramInlineKeyboardMarkup BuildSaveModeKeyboard(string locale)
            => new([[new TelegramInlineKeyboardButton("Ask", "savemode:ask")]]);

        public TelegramInlineKeyboardMarkup BuildStorageModeKeyboard(string locale)
            => new([[new TelegramInlineKeyboardButton("Graph", "storagemode:graph")]]);

        public TelegramInlineKeyboardMarkup BuildOneDriveKeyboard(string locale, bool isConnected, bool includeCheckStatusButton = false)
            => new([[new TelegramInlineKeyboardButton("Back", "settings:back")]]);

        public TelegramInlineKeyboardMarkup BuildOneDriveRebuildIndexConfirmationKeyboard(string locale)
            => new([[new TelegramInlineKeyboardButton("Start", CallbackDataConstants.OneDrive.RebuildIndexConfirm)]]);

        public TelegramInlineKeyboardMarkup BuildOneDriveClearCacheConfirmationKeyboard(string locale)
            => new([[new TelegramInlineKeyboardButton("Clear", CallbackDataConstants.OneDrive.ClearCacheConfirm)]]);

        public TelegramInlineKeyboardMarkup BuildVocabularySaveConfirmationKeyboard(string locale)
            => new([[new TelegramInlineKeyboardButton("Save", "vocab:save:yes"), new TelegramInlineKeyboardButton("Skip", "vocab:save:no")]]);

        public TelegramInlineKeyboardMarkup BuildVocabularyBatchSaveConfirmationKeyboard(string locale)
            => new(
            [
                [new TelegramInlineKeyboardButton("Save all", CallbackDataConstants.Vocab.SaveBatchYes)],
                [new TelegramInlineKeyboardButton("Skip all", CallbackDataConstants.Vocab.SaveBatchNo)]
            ]);

        public TelegramInlineKeyboardMarkup BuildVocabularyImportSourceKeyboard(string locale)
            => new(
            [
                [new TelegramInlineKeyboardButton("Photo", CallbackDataConstants.Vocab.ImportSourcePhoto), new TelegramInlineKeyboardButton("File", CallbackDataConstants.Vocab.ImportSourceFile)],
                [new TelegramInlineKeyboardButton("URL", CallbackDataConstants.Vocab.ImportSourceUrl), new TelegramInlineKeyboardButton("Text", CallbackDataConstants.Vocab.ImportSourceText)]
            ]);

        public TelegramInlineKeyboardMarkup BuildVocabularyUrlSelectionKeyboard(string locale)
            => new(
            [
                [new TelegramInlineKeyboardButton("Add all", CallbackDataConstants.Vocab.UrlSelectAll)],
                [new TelegramInlineKeyboardButton("Cancel", CallbackDataConstants.Vocab.UrlCancel)]
            ]);

        public TelegramInlineKeyboardMarkup BuildNotionKeyboard(string locale)
            => new([[new TelegramInlineKeyboardButton("Back", "settings:back")]]);

        public TelegramInlineKeyboardMarkup BuildMealCreateConfirmKeyboard(string locale)
            => new([[new TelegramInlineKeyboardButton("Create", CallbackDataConstants.Weekly.CreateConfirm), new TelegramInlineKeyboardButton("Cancel", CallbackDataConstants.Weekly.CreateCancel)]]);

        public TelegramInlineKeyboardMarkup BuildFoodPhotoConfirmKeyboard(string locale)
            => new([[new TelegramInlineKeyboardButton("Log this", CallbackDataConstants.Weekly.PhotoConfirm), new TelegramInlineKeyboardButton("Cancel", CallbackDataConstants.Weekly.PhotoCancel)]]);
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
        public sealed record SentMessage(long ChatId, string Text, TelegramSendOptions? Options, int? MessageThreadId);

        public int Calls { get; private set; }
        public int CallbackAnswers { get; private set; }
        public List<SentMessage> SentMessages { get; } = [];

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
            SentMessages.Add(new SentMessage(chatId, text, options, messageThreadId));
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
        public int DeleteCalls { get; private set; }
        public bool ThrowOnDelete { get; set; }
        public CancellationToken LastDeleteCancellationToken { get; private set; }

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
        {
            DeleteCalls++;
            LastDeleteCancellationToken = cancellationToken;

            if (ThrowOnDelete)
            {
                throw new InvalidOperationException("Simulated cleanup failure.");
            }

            return Task.CompletedTask;
        }
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
    public async Task Webhook_ShouldReturnOk_WhenCleanupFails()
    {
        var processedUpdates = new FakeTelegramProcessedUpdateRepository
        {
            ThrowOnDelete = true
        };

        var sut = CreateSut(
            new FakeConversationOrchestrator(),
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            new FakeTelegramFormatter("reply"),
            new FakeTelegramBotSender(),
            new TelegramOptions { Enabled = true },
            processedUpdates);

        var response = await sut.Webhook(
            BuildTextUpdate(chatId: 1001, userId: 2002, text: "void", messageThreadId: null),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);

        Assert.True(payload.Processed);
        Assert.True(payload.Replied);
        Assert.Equal(1, processedUpdates.MarkCalls);
        Assert.Equal(1, processedUpdates.DeleteCalls);
        Assert.False(processedUpdates.LastDeleteCancellationToken.CanBeCanceled);
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

    // ── Food: Shopping callbacks ──────────────────────────────────────────────

    [Theory]
    [InlineData(CallbackDataConstants.Shop.List, "food.shop.list")]
    [InlineData(CallbackDataConstants.Shop.Add, "food.shop.add.prompt")]
    public async Task Webhook_ShouldHandleShopCallback_AndReturnShoppingKeyboard(string callbackData, string expectedIntent)
    {
        var orchestrator = new FakeConversationOrchestrator
        {
            NextResult = ConversationAgentResult.Empty("food-tracking-agent", expectedIntent, "response")
        };
        var sender = new FakeTelegramBotSender();
        var navigationState = new FakeNavigationStateService { CurrentSection = NavigationSections.Shopping };
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
            new FakeTelegramFormatter("response"),
            sender,
            new TelegramOptions { Enabled = true },
            foodTrackingService: new FakeFoodTrackingService());

        var response = await sut.Webhook(BuildCallbackUpdate(1001, 2002, callbackData, null), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);
        Assert.True(payload.Processed);
        Assert.Equal(expectedIntent, payload.Intent);
        Assert.IsType<TelegramInlineKeyboardMarkup>(sender.LastOptions?.ReplyMarkup);
        Assert.Equal(1, sender.CallbackAnswers);
    }

    [Fact]
    public async Task Webhook_ShouldStartShoppingDeleteFlow_WhenDeleteCallbackRequested()
    {
        var foodService = new FakeFoodTrackingService
        {
            GroceryItems =
            [
                new GroceryListItemDto(1, "Milk", "2L", null, "Costco", false),
                new GroceryListItemDto(2, "Bread", null, null, null, false)
            ]
        };
        var sender = new FakeTelegramBotSender();
        var navigationState = new FakeNavigationStateService { CurrentSection = NavigationSections.Shopping };
        var sut = CreateSut(
            new FakeConversationOrchestrator(),
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            assistantSessionService: null,
            localeStateService: null,
            navigationStateService: navigationState,
            vocabularyCardRepository: null,
            navigationPresenter: null,
            new FakeTelegramFormatter("response"),
            sender,
            new TelegramOptions { Enabled = true },
            foodTrackingService: foodService);

        var response = await sut.Webhook(BuildCallbackUpdate(1001, 2002, CallbackDataConstants.Shop.Delete, null), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);
        Assert.True(payload.Processed);
        Assert.Equal("food.shop.delete.prompt", payload.Intent);
        Assert.Contains("Milk", sender.LastText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Cleared", sender.LastText, StringComparison.OrdinalIgnoreCase);
        Assert.IsType<TelegramInlineKeyboardMarkup>(sender.LastOptions?.ReplyMarkup);
    }

    // ── Food: Inventory callbacks/text ────────────────────────────────────────

    [Fact]
    public async Task Webhook_ShouldShowInventoryStats_WhenStatsCallbackRequested()
    {
        var foodService = new FakeFoodTrackingService
        {
            InventoryStats = new InventoryStatsDto(
                TotalItems: 12,
                WithCurrentQuantity: 10,
                WithMinQuantity: 8,
                LowStockItems: 3,
                TotalCurrentQuantity: 57.5m)
        };
        var sender = new FakeTelegramBotSender();
        var navigationState = new FakeNavigationStateService { CurrentSection = NavigationSections.Inventory };
        var sut = CreateSut(
            new FakeConversationOrchestrator(),
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            assistantSessionService: null,
            localeStateService: null,
            navigationStateService: navigationState,
            vocabularyCardRepository: null,
            navigationPresenter: null,
            new FakeTelegramFormatter(""),
            sender,
            new TelegramOptions { Enabled = true },
            foodTrackingService: foodService);

        var response = await sut.Webhook(BuildCallbackUpdate(1001, 2002, CallbackDataConstants.Inventory.Stats, null), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);
        Assert.Equal("inventory.stats", payload.Intent);
        Assert.Contains("12", sender.LastText, StringComparison.Ordinal);
        Assert.Contains("57.5", sender.LastText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Webhook_ShouldAdjustInventoryQuantity_WhenPendingAdjustAndValidTextProvided()
    {
        var foodService = new FakeFoodTrackingService
        {
            InventoryItems =
            [
                new FoodItemDto(7, "Beer", null, null, null, null) { CurrentQuantity = 5m }
            ]
        };
        var sender = new FakeTelegramBotSender();
        var navigationState = new FakeNavigationStateService { CurrentSection = NavigationSections.Inventory };
        var sut = CreateSut(
            new FakeConversationOrchestrator(),
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            assistantSessionService: null,
            localeStateService: null,
            navigationStateService: navigationState,
            vocabularyCardRepository: null,
            navigationPresenter: null,
            new FakeTelegramFormatter(""),
            sender,
            new TelegramOptions { Enabled = true },
            foodTrackingService: foodService);

        _ = await sut.Webhook(BuildCallbackUpdate(1001, 2002, CallbackDataConstants.Inventory.Adjust, null, updateId: 921), CancellationToken.None);
        var response = await sut.Webhook(BuildTextUpdate(1001, 2002, "7 -2", null, updateId: 922), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);
        Assert.Equal("inventory.adjust.done", payload.Intent);
        Assert.Equal(7, foodService.LastAdjustedInventoryItemId);
        Assert.Equal(-2m, foodService.LastAdjustedDelta);
    }

    [Fact]
    public async Task Webhook_ShouldRejectInvalidAdjustFormat_WhenPendingAdjustAndTextIsInvalid()
    {
        var foodService = new FakeFoodTrackingService
        {
            InventoryItems =
            [
                new FoodItemDto(9, "Milk", null, null, null, null) { CurrentQuantity = 2m }
            ]
        };
        var sender = new FakeTelegramBotSender();
        var navigationState = new FakeNavigationStateService { CurrentSection = NavigationSections.Inventory };
        var sut = CreateSut(
            new FakeConversationOrchestrator(),
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            assistantSessionService: null,
            localeStateService: null,
            navigationStateService: navigationState,
            vocabularyCardRepository: null,
            navigationPresenter: null,
            new FakeTelegramFormatter(""),
            sender,
            new TelegramOptions { Enabled = true },
            foodTrackingService: foodService);

        _ = await sut.Webhook(BuildCallbackUpdate(1001, 2002, CallbackDataConstants.Inventory.Adjust, null, updateId: 923), CancellationToken.None);
        var response = await sut.Webhook(BuildTextUpdate(1001, 2002, "9 2", null, updateId: 924), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);
        Assert.Equal("inventory.adjust.invalid", payload.Intent);
        Assert.Null(foodService.LastAdjustedInventoryItemId);
    }

    // ── Food: Weekly callbacks ────────────────────────────────────────────────

    [Theory]
    [InlineData(CallbackDataConstants.Weekly.View, "food.weekly.view")]
    [InlineData(CallbackDataConstants.Weekly.Plan, "food.weekly.cookable")]
    [InlineData(CallbackDataConstants.Weekly.Calories, "food.weekly.calories")]
    [InlineData(CallbackDataConstants.Weekly.Favourites, "food.weekly.favourites")]
    [InlineData(CallbackDataConstants.Weekly.Log, "food.weekly.log.prompt")]
    public async Task Webhook_ShouldHandleWeeklyCallback_AndReturnWeeklyKeyboard(string callbackData, string expectedIntent)
    {
        var orchestrator = new FakeConversationOrchestrator
        {
            NextResult = ConversationAgentResult.Empty("food-tracking-agent", expectedIntent, "response")
        };
        var sender = new FakeTelegramBotSender();
        var navigationState = new FakeNavigationStateService { CurrentSection = NavigationSections.WeeklyMenu };
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
            new FakeTelegramFormatter("response"),
            sender,
            new TelegramOptions { Enabled = true },
            foodTrackingService: new FakeFoodTrackingService());

        var response = await sut.Webhook(BuildCallbackUpdate(1001, 2002, callbackData, null), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);
        Assert.True(payload.Processed);
        Assert.Equal(expectedIntent, payload.Intent);
        Assert.IsType<TelegramInlineKeyboardMarkup>(sender.LastOptions?.ReplyMarkup);
    }

    // ── Food: Shopping text input ─────────────────────────────────────────────

    [Fact]
    public async Task Webhook_ShouldAddGroceryItemFromInventory_WhenTextInShoppingSection()
    {
        var foodService = new FakeFoodTrackingService
        {
            InventoryItems =
            [
                new FoodItemDto(1, "Milk", "Dairy", null, null, "2L")
            ]
        };
        var sender = new FakeTelegramBotSender();
        var navigationState = new FakeNavigationStateService { CurrentSection = NavigationSections.Shopping };
        var sut = CreateSut(
            new FakeConversationOrchestrator(),
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            assistantSessionService: null,
            localeStateService: null,
            navigationStateService: navigationState,
            vocabularyCardRepository: null,
            navigationPresenter: null,
            new FakeTelegramFormatter(""),
            sender,
            new TelegramOptions { Enabled = true },
            foodTrackingService: foodService);

        var response = await sut.Webhook(BuildTextUpdate(1001, 2002, "Milk | qty:2L | store:Costco", null), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);
        Assert.Equal("food.shop.added", payload.Intent);
        Assert.Contains("Milk", sender.LastText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Matched inventory item", sender.LastText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal([1], foodService.LastAddFromInventoryIds);
        Assert.IsType<TelegramInlineKeyboardMarkup>(sender.LastOptions?.ReplyMarkup);
    }

    [Fact]
    public async Task Webhook_ShouldRejectCyrillicName_WhenTextInShoppingSection()
    {
        var foodService = new FakeFoodTrackingService();
        var sender = new FakeTelegramBotSender();
        var navigationState = new FakeNavigationStateService { CurrentSection = NavigationSections.Shopping };
        var sut = CreateSut(
            new FakeConversationOrchestrator(),
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            assistantSessionService: null,
            localeStateService: null,
            navigationStateService: navigationState,
            vocabularyCardRepository: null,
            navigationPresenter: null,
            new FakeTelegramFormatter(""),
            sender,
            new TelegramOptions { Enabled = true },
            foodTrackingService: foodService);

        var response = await sut.Webhook(BuildTextUpdate(1001, 2002, "Молоко 2л", null), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);
        Assert.Equal("shop.only_english", payload.Intent);
        Assert.Contains("must be in English", sender.LastText, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(foodService.LastAddFromInventoryIds);
    }

    [Fact]
    public async Task Webhook_ShouldNotAdd_WhenNameIsMissingInInventory()
    {
        var foodService = new FakeFoodTrackingService
        {
            InventoryItems =
            [
                new FoodItemDto(10, "Banana", "Fruit", null, null, "5")
            ]
        };
        var sender = new FakeTelegramBotSender();
        var navigationState = new FakeNavigationStateService { CurrentSection = NavigationSections.Shopping };
        var sut = CreateSut(
            new FakeConversationOrchestrator(),
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            assistantSessionService: null,
            localeStateService: null,
            navigationStateService: navigationState,
            vocabularyCardRepository: null,
            navigationPresenter: null,
            new FakeTelegramFormatter(""),
            sender,
            new TelegramOptions { Enabled = true },
            foodTrackingService: foodService);

        var response = await sut.Webhook(BuildTextUpdate(1001, 2002, "Apple 2kg", null), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);
        Assert.Equal("shop.not_in_inventory", payload.Intent);
        Assert.Contains("not found in inventory", sender.LastText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Add this product to inventory first", sender.LastText, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(foodService.LastAddFromInventoryIds);
    }

    [Fact]
    public async Task Webhook_ShouldDeleteSelectedShoppingItemByName_WhenDeleteFlowIsPending()
    {
        var foodService = new FakeFoodTrackingService
        {
            GroceryItems =
            [
                new GroceryListItemDto(1, "Milk", "2L", null, "Costco", false),
                new GroceryListItemDto(2, "Bread", null, null, null, false)
            ]
        };
        var sender = new FakeTelegramBotSender();
        var navigationState = new FakeNavigationStateService { CurrentSection = NavigationSections.Shopping };
        var sut = CreateSut(
            new FakeConversationOrchestrator(),
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            assistantSessionService: null,
            localeStateService: null,
            navigationStateService: navigationState,
            vocabularyCardRepository: null,
            navigationPresenter: null,
            new FakeTelegramFormatter(""),
            sender,
            new TelegramOptions { Enabled = true },
            foodTrackingService: foodService);

        _ = await sut.Webhook(BuildCallbackUpdate(1001, 2002, CallbackDataConstants.Shop.Delete, null, updateId: 8101), CancellationToken.None);
        var response = await sut.Webhook(BuildTextUpdate(1001, 2002, "Milk", null, updateId: 8102), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);
        Assert.Equal("food.shop.delete.done", payload.Intent);
        Assert.Equal([1], foodService.LastDeletedItemIds);
        Assert.Contains("1", sender.LastText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Webhook_ShouldReturnStubResponse_WhenTextInShoppingSection_AndFoodServiceNull()
    {
        var sender = new FakeTelegramBotSender();
        var navigationState = new FakeNavigationStateService { CurrentSection = NavigationSections.Shopping };
        var sut = CreateSut(
            new FakeConversationOrchestrator(),
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            assistantSessionService: null,
            localeStateService: null,
            navigationStateService: navigationState,
            vocabularyCardRepository: null,
            navigationPresenter: null,
            new FakeTelegramFormatter(""),
            sender,
            new TelegramOptions { Enabled = true }
            /* foodTrackingService: null by default */);

        var response = await sut.Webhook(BuildTextUpdate(1001, 2002, "Milk", null), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);
        Assert.Equal("shopping.text", payload.Intent);
    }

    // ── Food: Weekly text input – meal logging ────────────────────────────────

    [Fact]
    public async Task Webhook_ShouldLogMeal_WhenNumberTextInWeeklySection()
    {
        var foodService = new FakeFoodTrackingService
        {
            LogMealResult = 42
        };
        var sender = new FakeTelegramBotSender();
        var navigationState = new FakeNavigationStateService { CurrentSection = NavigationSections.WeeklyMenu };
        var sut = CreateSut(
            new FakeConversationOrchestrator(),
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            assistantSessionService: null,
            localeStateService: null,
            navigationStateService: navigationState,
            vocabularyCardRepository: null,
            navigationPresenter: null,
            new FakeTelegramFormatter(""),
            sender,
            new TelegramOptions { Enabled = true },
            foodTrackingService: foodService);

        var response = await sut.Webhook(BuildTextUpdate(1001, 2002, "5 2", null), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);
        Assert.Equal("food.weekly.logged", payload.Intent);
        Assert.Contains("5", sender.LastText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(5, foodService.LastLoggedMealId);
        Assert.Equal(2m, foodService.LastLoggedServings);
    }

    [Fact]
    public async Task Webhook_ShouldReturnNotFound_WhenMealIdNotFound_InWeeklySection()
    {
        var foodService = new FakeFoodTrackingService { ThrowOnLog = true };
        var sender = new FakeTelegramBotSender();
        var navigationState = new FakeNavigationStateService { CurrentSection = NavigationSections.WeeklyMenu };
        var sut = CreateSut(
            new FakeConversationOrchestrator(),
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            assistantSessionService: null,
            localeStateService: null,
            navigationStateService: navigationState,
            vocabularyCardRepository: null,
            navigationPresenter: null,
            new FakeTelegramFormatter(""),
            sender,
            new TelegramOptions { Enabled = true },
            foodTrackingService: foodService);

        var response = await sut.Webhook(BuildTextUpdate(1001, 2002, "999", null), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);
        Assert.Equal("food.weekly.log.not_found", payload.Intent);
    }

    [Fact]
    public async Task Webhook_ShouldShowMealList_WhenNonNumericTextInWeeklySection()
    {
        var foodService = new FakeFoodTrackingService
        {
            Meals = [new MealDto(1, "Soup", 300, null, null, null, 20, 2, [])]
        };
        var sender = new FakeTelegramBotSender();
        var navigationState = new FakeNavigationStateService { CurrentSection = NavigationSections.WeeklyMenu };
        var sut = CreateSut(
            new FakeConversationOrchestrator(),
            new FakeConversationScopeAccessor(),
            new FakeVocabularyStorageModeProvider(),
            new FakeVocabularyStoragePreferenceService(),
            assistantSessionService: null,
            localeStateService: null,
            navigationStateService: navigationState,
            vocabularyCardRepository: null,
            navigationPresenter: null,
            new FakeTelegramFormatter(""),
            sender,
            new TelegramOptions { Enabled = true },
            foodTrackingService: foodService);

        var response = await sut.Webhook(BuildTextUpdate(1001, 2002, "what can I eat?", null), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<TelegramWebhookResponse>(ok.Value);
        Assert.Equal("food.weekly.view", payload.Intent);
        Assert.Contains("Soup", sender.LastText, StringComparison.OrdinalIgnoreCase);
    }

    // ── Food: FakeFoodTrackingService ─────────────────────────────────────────

    private sealed class FakeFoodTrackingService : IFoodTrackingService
    {
        public IReadOnlyList<FoodItemDto> InventoryItems { get; init; } = [];
        public IReadOnlyList<GroceryListItemDto> GroceryItems { get; init; } = [];
        public IReadOnlyList<MealDto> Meals { get; init; } = [];
        public IReadOnlyList<MealDto> CookableMeals { get; init; } = [];
        public IReadOnlyList<MealFrequency> FavouriteMeals { get; init; } = [];
        public CalorieSummary CalorieSummary { get; init; } = new(DateTime.MinValue, DateTime.MaxValue, 0, 0, 0, 0, 0);
        public int ClearedCount { get; init; }
        public int DeletedByIdsCount { get; init; }
        public int LogMealResult { get; init; } = 1;
        public bool ThrowOnLog { get; init; }
        public InventoryStatsDto? InventoryStats { get; init; }

        public int LastLoggedMealId { get; private set; }
        public decimal LastLoggedServings { get; private set; }
        public IReadOnlyCollection<int> LastDeletedItemIds { get; private set; } = [];
        public IReadOnlyCollection<int> LastAddFromInventoryIds { get; private set; } = [];
        public int? LastAdjustedInventoryItemId { get; private set; }
        public decimal? LastAdjustedDelta { get; private set; }

        public Task<IReadOnlyList<FoodItemDto>> GetAllInventoryAsync(int take = 50, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FoodItemDto>>(InventoryItems.Take(take).ToList());

        public Task<IReadOnlyList<FoodItemDto>> SearchInventoryAsync(string query, int take = 10, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return GetAllInventoryAsync(take, cancellationToken);
            }

            var matches = InventoryItems
                .Where(x => x.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Take(take)
                .ToList();
            return Task.FromResult<IReadOnlyList<FoodItemDto>>(matches);
        }

        public Task<InventoryStatsDto> GetInventoryStatsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(InventoryStats ?? new InventoryStatsDto(InventoryItems.Count, 0, 0, 0, 0m));

        public Task<FoodItemDto> AdjustInventoryQuantityAsync(int foodItemId, decimal delta, CancellationToken cancellationToken = default)
        {
            var found = InventoryItems.FirstOrDefault(x => x.Id == foodItemId);
            if (found is null)
            {
                throw new InvalidOperationException($"Food item {foodItemId} not found.");
            }

            LastAdjustedInventoryItemId = foodItemId;
            LastAdjustedDelta = delta;
            return Task.FromResult(found with { CurrentQuantity = (found.CurrentQuantity ?? 0m) + delta });
        }

        public Task<GroceryListItemDto> AddToShoppingFromInventoryAsync(int foodItemId, string? quantity, string? store, CancellationToken cancellationToken = default)
        {
            var found = InventoryItems.FirstOrDefault(x => x.Id == foodItemId);
            if (found is null)
            {
                throw new InvalidOperationException($"Food item {foodItemId} not found.");
            }

            LastAddFromInventoryIds = LastAddFromInventoryIds.Concat([foodItemId]).ToArray();
            return Task.FromResult(new GroceryListItemDto(100 + foodItemId, found.Name, quantity, null, store, false));
        }

        public Task<IReadOnlyList<FoodItemDto>> GetLowStockItemsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FoodItemDto>>(
                InventoryItems
                    .Where(x => x.MinQuantity.HasValue && x.CurrentQuantity.HasValue && x.CurrentQuantity.Value < x.MinQuantity.Value)
                    .ToList());

        public Task<IReadOnlyList<GroceryListItemDto>> GetActiveGroceryListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(GroceryItems);

        public Task<GroceryListItemDto> AddGroceryItemAsync(string name, string? quantity, string? store, CancellationToken cancellationToken = default)
            => Task.FromResult(new GroceryListItemDto(99, name, quantity, null, store, false));

        public Task<int> MarkItemsBoughtAsync(IReadOnlyCollection<int> itemIds, CancellationToken cancellationToken = default)
            => Task.FromResult(itemIds.Count);

        public Task<int> MarkAllBoughtAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<int> ClearBoughtItemsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(ClearedCount);

        public Task<int> DeleteItemsByIdsAsync(IReadOnlyCollection<int> itemIds, CancellationToken cancellationToken = default)
        {
            LastDeletedItemIds = itemIds.ToArray();
            return Task.FromResult(DeletedByIdsCount == 0 ? itemIds.Count : DeletedByIdsCount);
        }

        public Task<IReadOnlyList<MealDto>> GetAllMealsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Meals);

        public Task<IReadOnlyList<MealDto>> GetCookableNowAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CookableMeals);

        public Task<IReadOnlyList<MealFrequency>> GetFavouriteMealsAsync(int take = 5, CancellationToken cancellationToken = default)
            => Task.FromResult(FavouriteMeals);

        public Task<int> LogMealAsync(int mealId, decimal servings, string? notes, CancellationToken cancellationToken = default)
        {
            if (ThrowOnLog) throw new InvalidOperationException($"Meal {mealId} not found.");
            LastLoggedMealId = mealId;
            LastLoggedServings = servings;
            return Task.FromResult(LogMealResult);
        }

        public Task<CalorieSummary> GetCalorieSummaryAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
            => Task.FromResult(CalorieSummary);

        public Task<MealDto> CreateMealAsync(string name, int? caloriesPerServing, decimal? proteinGrams, decimal? carbsGrams, decimal? fatGrams, int? prepTimeMinutes, int defaultServings, IReadOnlyList<(string Name, string? Quantity)> ingredients, CancellationToken cancellationToken = default)
            => Task.FromResult(new MealDto(99, name, caloriesPerServing, proteinGrams, carbsGrams, fatGrams, prepTimeMinutes, defaultServings, []));

        public int LastQuickLogCalories { get; private set; }
        public string? LastQuickLogName { get; private set; }

        public Task<int> LogQuickMealAsync(string name, int calories, decimal servings, CancellationToken cancellationToken = default)
        {
            LastQuickLogName = name;
            LastQuickLogCalories = calories;
            return Task.FromResult(1);
        }

        public Task<DailyProgressDto> GetDailyProgressAsync(int calorieGoal, CancellationToken cancellationToken = default)
            => Task.FromResult(new DailyProgressDto(calorieGoal, 800, calorieGoal - 800, 40m, 3));

        public Task<DietDiversityDto> GetDietDiversityAsync(int days = 7, CancellationToken cancellationToken = default)
            => Task.FromResult(new DietDiversityDto(days, 4, 10, ["Oatmeal"], ["Oatmeal", "Salad", "Pasta", "Soup"]));

        public Task<PortionCalculationDto?> CalculatePortionsAsync(int mealId, int targetServings, CancellationToken cancellationToken = default)
            => Task.FromResult<PortionCalculationDto?>(new PortionCalculationDto("Test Meal", 2, targetServings, (decimal)targetServings / 2, []));
    }
}
