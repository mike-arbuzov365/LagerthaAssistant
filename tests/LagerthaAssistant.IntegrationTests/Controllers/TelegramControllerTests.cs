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
using LagerthaAssistant.Application.Interfaces.Repositories;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.AI;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Models.Localization;
using LagerthaAssistant.Application.Navigation;
using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.Domain.Entities;
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
        Assert.NotEqual("ru", localeState.StoredLocale);
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
        Assert.Contains("index appears empty", sender.LastText, StringComparison.OrdinalIgnoreCase);
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
        Assert.Contains("Index rebuilt", sender.LastText, StringComparison.OrdinalIgnoreCase);
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
    public async Task Webhook_ShouldShowSaveSetupHint_WhenGraphAuthRequiredOnSave()
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
        Assert.Equal("vocab.save.retry", payload.Intent);
        Assert.Contains("Щоб зберігати слова", sender.LastText, StringComparison.Ordinal);
        Assert.DoesNotContain("Run /graph login", sender.LastText, StringComparison.OrdinalIgnoreCase);
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
        FakeVocabularyDeckService? vocabularyDeckService = null)
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
            vocabularyDeckService: vocabularyDeckService);
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
        FakeVocabularyDeckService? vocabularyDeckService = null)
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
            vocabularyDeckService: vocabularyDeckService);
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
        FakeVocabularyDeckService? vocabularyDeckService = null)
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
            graphAuthService ?? new FakeGraphAuthService(),
            navigationPresenter ?? new FakeTelegramNavigationPresenter(),
            formatter,
            sender,
            processedUpdates ?? new FakeTelegramProcessedUpdateRepository(),
            Options.Create(options),
            NullLogger<TelegramController>.Instance);
    }

    private static TelegramWebhookUpdateRequest BuildTextUpdate(
        long chatId,
        long userId,
        string text,
        int? messageThreadId,
        string? languageCode = "en")
    {
        return new TelegramWebhookUpdateRequest(
            UpdateId: 1,
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

    private static ConversationAgentResult BuildVocabularySingleResult()
    {
        var item = new ConversationAgentItemResult(
            Input: "smile",
            Lookup: new VocabularyLookupResult("smile", []),
            AssistantCompletion: new AssistantCompletionResult(
                "smile\n\n(v) усміхатися\n\nShe smiled after fixing the bug.",
                "test-model",
                Usage: null),
            AppendPreview: new VocabularyAppendPreviewResult(
                Status: VocabularyAppendPreviewStatus.ReadyToAppend,
                Word: "smile",
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

        public IReadOnlyList<VocabularyDeckEntry> LastEntries { get; private set; } = [];

        public VocabularyStorageMode LastMode { get; private set; } = VocabularyStorageMode.Local;

        public int RebuildResult { get; set; }

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
            => Task.FromResult(0);

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

        public Task<VocabularyLookupResult> FindInWritableDecksAsync(string word, CancellationToken cancellationToken = default)
            => Task.FromResult(new VocabularyLookupResult(word, []));

        public Task<IReadOnlyList<VocabularyDeckFile>> GetWritableDeckFilesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<VocabularyDeckFile>>([]);

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
                "vocab.url.prompt" => "Send URL",
                "vocab.list.empty" => "Empty",
                "vocab.list.title" => "Latest words",
                "settings.title" => "Settings",
                "settings.language" => "Language",
                "settings.save_mode" => "Save mode",
                "settings.storage_mode" => "Storage mode",
                "settings.onedrive" => "OneDrive / Graph",
                "settings.notion" => "Notion (coming soon)",
                "savemode.title" => "Save mode: {0}",
                "savemode.changed" => "Changed to {0}",
                "storagemode.title" => "Storage mode: {0}",
                "storagemode.changed" => "Changed storage to {0}",
                "vocab.save.ask" => "Save \"{0}\" to \"{1}\"?",
                "vocab.save.saved" => "Saved to {0} (row {1}).",
                "vocab.save.duplicate" => "Duplicate",
                "vocab.save.skip" => "Save skipped",
                "vocab.save_failed" => "Save failed: {0}",
                "vocab.graph_save_setup_required" => "To save words, open Settings -> OneDrive / Graph, sign in, and try again.",
                "vocab.save_batch_ask_hint" => "Batch ask hint",
                "vocab.save_mode_off_hint" => "Save mode off hint",
                "vocab.save_yes" => "Save",
                "vocab.save_no" => "Skip",
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
                "onedrive.rebuild_index" => "Rebuild index",
                "onedrive.rebuild_index_warning" => "Rebuilding index can take some time. Start now?",
                "onedrive.rebuild_index_start" => "Start rebuild",
                "onedrive.rebuild_index_started" => "Rebuilding index started...",
                "onedrive.rebuild_index_suggest" => "Tip: index appears empty. Rebuild it.",
                "onedrive.sync_now_done" => "Sync complete: completed={0}, requeued={1}, failed={2}, pending={3}.",
                "onedrive.rebuild_index_done" => "Index rebuilt from writable decks: scanned={0}, indexed={1}.",
                "onedrive.operation_failed" => "Operation failed: {0}",
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
            => new([
                [new TelegramKeyboardButton("Chat"), new TelegramKeyboardButton("Vocabulary")],
                [new TelegramKeyboardButton("Shopping"), new TelegramKeyboardButton("Menu")],
                [new TelegramKeyboardButton("Settings")]
            ]);

        public TelegramInlineKeyboardMarkup BuildVocabularyKeyboard(string locale)
            => new([[new TelegramInlineKeyboardButton("Add", "vocab:add")]]);

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

        public TelegramInlineKeyboardMarkup BuildVocabularySaveConfirmationKeyboard(string locale)
            => new([[new TelegramInlineKeyboardButton("Save", "vocab:save:yes"), new TelegramInlineKeyboardButton("Skip", "vocab:save:no")]]);

        public TelegramInlineKeyboardMarkup BuildNotionKeyboard(string locale)
            => new([[new TelegramInlineKeyboardButton("Back", "settings:back")]]);
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
}

