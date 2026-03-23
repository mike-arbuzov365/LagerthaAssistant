using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using LagerthaAssistant.Api.Contracts;
using LagerthaAssistant.Api.Interfaces;
using LagerthaAssistant.Api.Models;
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
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.Application.Navigation;
using LagerthaAssistant.Application.Services.Vocabulary;
using LagerthaAssistant.Domain.Entities;
using LagerthaAssistant.Infrastructure.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace LagerthaAssistant.Api.Controllers;

[ApiController]
[Route("api/telegram")]
public sealed class TelegramController : ControllerBase
{
    private const string TelegramChannel = "telegram";
    private const string TelegramSecretHeader = "X-Telegram-Bot-Api-Secret-Token";
    private const string HtmlParseMode = "HTML";
    private const string SectionSeparator = "--------------------";
    private const string BatchItemMarker = "🔹";
    private const string QuestionMarker = "❓";
    private static readonly string[] WarningMarkers = ["⚠️", "⚠"];
    private const int ManualSyncBatchSize = 25;
    private const int ManualSyncMaxPasses = 5;
    private static readonly Regex UrlLikeRegex = new("^https?://", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SelectionNumberRegex = new(@"\d+", RegexOptions.Compiled);
    private static readonly Regex LeadingDecorationRegex = new("^[^\\p{L}\\p{N}]+", RegexOptions.Compiled);
    private static readonly Regex SentenceSplitRegex = new("(?<=[\\.!\\?])\\s+", RegexOptions.Compiled);
    private static readonly Regex LeadingWordSeparatorRegex = new("^[\\s:;,.!\\-–—]+", RegexOptions.Compiled);
    private static readonly string[] PrimaryPartOfSpeechMarkers = ["n", "v", "pv", "iv", "adv", "prep"];
    private static readonly string[] UrlSelectionPosOrder = ["n", "v", "adj"];
    private static readonly string AutomaticReleaseVersionToken = "auto";
    private static readonly string[] ReleaseVersionEnvironmentKeys =
    [
        "RELEASE_ANNOUNCEMENT_VERSION",
        "RELEASE_VERSION",
        "RAILWAY_GIT_COMMIT_SHA",
        "RAILWAY_DEPLOYMENT_ID"
    ];
    private static readonly string[] ReleaseDateEnvironmentKeys =
    [
        "RELEASE_DATE",
        "RAILWAY_DEPLOYMENT_CREATED_AT",
        "RAILWAY_DEPLOYED_AT"
    ];
    private static readonly HashSet<string> UrlSelectAllTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "all", "всі", "усі", "todo", "todos", "tous", "alle", "wszystkie"
    };
    private static readonly HashSet<string> UrlCancelTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "cancel", "stop", "no", "ні", "cancelar", "annuler", "abbrechen", "anuluj"
    };

    private readonly TelegramPendingStateStore _pendingStateStore;

    private readonly IConversationOrchestrator _orchestrator;
    private readonly IAssistantSessionService _assistantSessionService;
    private readonly IConversationScopeAccessor _scopeAccessor;
    private readonly IVocabularyStorageModeProvider _storageModeProvider;
    private readonly IVocabularyStoragePreferenceService _storagePreferenceService;
    private readonly IUserLocaleStateService _userLocaleStateService;
    private readonly INavigationStateService _navigationStateService;
    private readonly NavigationRouter _navigationRouter;
    private readonly IVocabularyCardRepository _vocabularyCardRepository;
    private readonly IVocabularySaveModePreferenceService _saveModePreferenceService;
    private readonly IVocabularyPersistenceService _vocabularyPersistenceService;
    private readonly IVocabularySyncProcessor _vocabularySyncProcessor;
    private readonly IVocabularyIndexService _vocabularyIndexService;
    private readonly IVocabularyDeckService _vocabularyDeckService;
    private readonly IVocabularyReplyParser _vocabularyReplyParser;
    private readonly IVocabularyDiscoveryService _vocabularyDiscoveryService;
    private readonly ITelegramImportSourceReader _importSourceReader;
    private readonly VocabularyDeckOptions _vocabularyDeckOptions;
    private readonly IGraphAuthService _graphAuthService;
    private readonly ITelegramNavigationPresenter _navigationPresenter;
    private readonly ITelegramConversationResponseFormatter _responseFormatter;
    private readonly ITelegramBotSender _telegramBotSender;
    private readonly ITelegramProcessedUpdateRepository _processedUpdates;
    private readonly IFoodTrackingService? _foodTrackingService;
    private readonly IUserMemoryRepository? _userMemoryRepository;
    private readonly IUnitOfWork? _unitOfWork;
    private readonly ReleaseAnnouncementOptions _releaseAnnouncementOptions;
    private readonly NotionOptions _notionOptions;
    private readonly NotionFoodOptions _notionFoodOptions;
    private readonly NotionSyncWorkerOptions _notionSyncWorkerOptions;
    private readonly FoodSyncWorkerOptions _foodSyncWorkerOptions;
    private readonly TelegramOptions _options;
    private readonly ILogger<TelegramController> _logger;

    public TelegramController(
        IConversationOrchestrator orchestrator,
        IAssistantSessionService assistantSessionService,
        IConversationScopeAccessor scopeAccessor,
        IVocabularyStorageModeProvider storageModeProvider,
        IVocabularyStoragePreferenceService storagePreferenceService,
        IUserLocaleStateService userLocaleStateService,
        INavigationStateService navigationStateService,
        NavigationRouter navigationRouter,
        IVocabularyCardRepository vocabularyCardRepository,
        IVocabularySaveModePreferenceService saveModePreferenceService,
        IVocabularyPersistenceService vocabularyPersistenceService,
        IVocabularySyncProcessor vocabularySyncProcessor,
        IVocabularyIndexService vocabularyIndexService,
        IVocabularyDeckService vocabularyDeckService,
        IVocabularyReplyParser vocabularyReplyParser,
        IVocabularyDiscoveryService vocabularyDiscoveryService,
        ITelegramImportSourceReader importSourceReader,
        VocabularyDeckOptions vocabularyDeckOptions,
        IGraphAuthService graphAuthService,
        ITelegramNavigationPresenter navigationPresenter,
        ITelegramConversationResponseFormatter responseFormatter,
        ITelegramBotSender telegramBotSender,
        ITelegramProcessedUpdateRepository processedUpdates,
        IOptions<TelegramOptions> options,
        ILogger<TelegramController> logger,
        IUserMemoryRepository? userMemoryRepository = null,
        IUnitOfWork? unitOfWork = null,
        IOptions<ReleaseAnnouncementOptions>? releaseAnnouncementOptions = null,
        NotionOptions? notionOptions = null,
        NotionFoodOptions? notionFoodOptions = null,
        IOptions<NotionSyncWorkerOptions>? notionSyncWorkerOptions = null,
        IOptions<FoodSyncWorkerOptions>? foodSyncWorkerOptions = null,
        IFoodTrackingService? foodTrackingService = null,
        TelegramPendingStateStore? pendingStateStore = null)
    {
        _pendingStateStore = pendingStateStore ?? new TelegramPendingStateStore();
        _orchestrator = orchestrator;
        _assistantSessionService = assistantSessionService;
        _scopeAccessor = scopeAccessor;
        _storageModeProvider = storageModeProvider;
        _storagePreferenceService = storagePreferenceService;
        _userLocaleStateService = userLocaleStateService;
        _navigationStateService = navigationStateService;
        _navigationRouter = navigationRouter;
        _vocabularyCardRepository = vocabularyCardRepository;
        _saveModePreferenceService = saveModePreferenceService;
        _vocabularyPersistenceService = vocabularyPersistenceService;
        _vocabularySyncProcessor = vocabularySyncProcessor;
        _vocabularyIndexService = vocabularyIndexService;
        _vocabularyDeckService = vocabularyDeckService;
        _vocabularyReplyParser = vocabularyReplyParser;
        _vocabularyDiscoveryService = vocabularyDiscoveryService;
        _importSourceReader = importSourceReader;
        _vocabularyDeckOptions = vocabularyDeckOptions;
        _graphAuthService = graphAuthService;
        _navigationPresenter = navigationPresenter;
        _responseFormatter = responseFormatter;
        _telegramBotSender = telegramBotSender;
        _processedUpdates = processedUpdates;
        _userMemoryRepository = userMemoryRepository;
        _unitOfWork = unitOfWork;
        _foodTrackingService = foodTrackingService;
        _releaseAnnouncementOptions = releaseAnnouncementOptions?.Value ?? new ReleaseAnnouncementOptions();
        _notionOptions = notionOptions ?? new NotionOptions();
        _notionFoodOptions = notionFoodOptions ?? new NotionFoodOptions();
        _notionSyncWorkerOptions = notionSyncWorkerOptions?.Value ?? new NotionSyncWorkerOptions();
        _foodSyncWorkerOptions = foodSyncWorkerOptions?.Value ?? new FoodSyncWorkerOptions();
        _options = options.Value;
        _logger = logger;

        if (_options.Enabled && string.IsNullOrWhiteSpace(_options.WebhookSecret))
        {
            _logger.LogWarning("Telegram webhook secret is not configured — all incoming requests will be accepted.");
        }
    }

    [HttpPost("webhook")]
    [EnableRateLimiting("telegram-webhook")]
    [ProducesResponseType(typeof(TelegramWebhookResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TelegramWebhookResponse>> Webhook(
        [FromBody] TelegramWebhookUpdateRequest update,
        CancellationToken cancellationToken = default)
    {
        if (!IsSecretValid())
        {
            return Unauthorized();
        }

        string? callbackQueryId = update?.CallbackQuery?.Id;

        try
        {
            if (!_options.Enabled)
            {
                return Ok(new TelegramWebhookResponse(false, false, null, "Telegram integration is disabled."));
            }

            if (update is null || !ApiTelegramUpdateMapper.TryMapInbound(update, out var inbound))
            {
                return Ok(new TelegramWebhookResponse(false, false, null, "Ignored: unsupported update."));
            }

            if (!string.IsNullOrWhiteSpace(callbackQueryId))
            {
                await TryAnswerCallbackQueryAsync(callbackQueryId, CancellationToken.None);
                callbackQueryId = null;
            }

            if (await _processedUpdates.IsProcessedAsync(update.UpdateId, cancellationToken))
            {
                _logger.LogDebug("Telegram update {UpdateId} already processed; skipping.", update.UpdateId);
                return Ok(new TelegramWebhookResponse(Processed: true, Replied: false, Intent: null, Error: null));
            }

            var scope = ApiConversationScopeApplier.Apply(
                _scopeAccessor,
                TelegramChannel,
                inbound.UserId,
                inbound.ConversationId);

            _pendingStateStore.CleanupIfOversized();

            // Telegram bot works only with Graph storage mode.
            _storageModeProvider.SetMode(VocabularyStorageMode.Graph);

            var currentSection = await _navigationStateService.GetCurrentSectionAsync(
                scope.Channel,
                scope.UserId,
                scope.ConversationId,
                cancellationToken);

            var storedLocale = await _userLocaleStateService.GetStoredLocaleAsync(scope.Channel, scope.UserId, cancellationToken);
            var routeLocale = storedLocale ?? LocalizationConstants.NormalizeLocaleCode(inbound.LanguageCode);
            var labels = _navigationPresenter.GetMainMenuLabels(routeLocale);
            var route = _navigationRouter.Resolve(
                new NavigationRouteInput(inbound.Text, inbound.CallbackData, currentSection),
                labels);

            var inOnboardingFlow = string.IsNullOrWhiteSpace(storedLocale)
                && (route.Kind == NavigationRouteKind.Start
                    || route.Kind == NavigationRouteKind.LanguageOnboardingText
                    || IsLanguageCallback(route.CallbackData));

            var localeState = inOnboardingFlow
                ? new Application.Models.Localization.UserLocaleStateResult(routeLocale, IsInitialized: false, IsSwitched: false)
                : await _userLocaleStateService.EnsureLocaleAsync(
                    scope.Channel,
                    scope.UserId,
                    inbound.LanguageCode,
                    inbound.IsCallback ? null : inbound.Text,
                    cancellationToken);

            var response = await HandleRouteAsync(
                route,
                inbound,
                scope,
                localeState.Locale,
                currentSection,
                hasStoredLocale: !string.IsNullOrWhiteSpace(storedLocale),
                cancellationToken);

            var outboundText = response.IsHtml
                ? response.Text
                : WebUtility.HtmlEncode(response.Text);

            if (localeState.IsSwitched)
            {
                var switchedText = WebUtility.HtmlEncode(_navigationPresenter.GetText("locale.switched", localeState.Locale));
                outboundText = string.Concat(switchedText, Environment.NewLine, Environment.NewLine, outboundText);
            }

            outboundText = await PrependReleaseAnnouncementIfNeededAsync(
                scope,
                localeState.Locale,
                outboundText,
                cancellationToken);

            var sendResult = await _telegramBotSender.SendTextAsync(
                inbound.ChatId,
                outboundText,
                EnsureHtmlParseMode(response.Options),
                inbound.MessageThreadId,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(response.FollowUpMainKeyboardLocale))
            {
                await SendMainKeyboardRefreshMessageAsync(
                    inbound.ChatId,
                    response.FollowUpMainKeyboardLocale,
                    inbound.MessageThreadId,
                    CancellationToken.None);
            }

            await _processedUpdates.MarkProcessedAsync(update.UpdateId, cancellationToken);
            await CleanupOldUpdatesAsync(CancellationToken.None);

            if (!sendResult.Succeeded)
            {
                _logger.LogWarning(
                    "Telegram reply send failed. ChatId={ChatId}; UserId={UserId}; ConversationId={ConversationId}; Error={Error}",
                    inbound.ChatId,
                    scope.UserId,
                    scope.ConversationId,
                    sendResult.ErrorMessage);

                return Ok(new TelegramWebhookResponse(Processed: true, Replied: false, Intent: response.Intent, Error: sendResult.ErrorMessage));
            }

            return Ok(new TelegramWebhookResponse(
                Processed: true,
                Replied: true,
                Intent: response.Intent,
                Error: null));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while processing Telegram update {UpdateId}.", update?.UpdateId);
            return Ok(new TelegramWebhookResponse(false, false, null, "Unexpected webhook processing error."));
        }
        finally
        {
            await TryAnswerCallbackQueryAsync(callbackQueryId, CancellationToken.None);
        }
    }

    private async Task<TelegramRouteResponse> HandleRouteAsync(
        NavigationRoute route,
        TelegramInboundMessage inbound,
        ConversationScope scope,
        string locale,
        string currentSection,
        bool hasStoredLocale,
        CancellationToken cancellationToken)
    {
        switch (route.Kind)
        {
            case NavigationRouteKind.Start:
                if (!hasStoredLocale)
                {
                    await _navigationStateService.SetCurrentSectionAsync(scope.Channel, scope.UserId, scope.ConversationId, NavigationSections.LanguageOnboarding, cancellationToken);
                    return BuildOnboardingLanguagePickerResponse(locale);
                }

                await _navigationStateService.SetCurrentSectionAsync(scope.Channel, scope.UserId, scope.ConversationId, NavigationSections.Main, cancellationToken);
                return new TelegramRouteResponse(
                    "nav.start",
                    _navigationPresenter.GetText("start.welcome", locale),
                    ReplyKeyboard(_navigationPresenter.BuildMainReplyKeyboard(locale)));

            case NavigationRouteKind.MainChatButton:
                await _navigationStateService.SetCurrentSectionAsync(scope.Channel, scope.UserId, scope.ConversationId, NavigationSections.Chat, cancellationToken);
                _pendingStateStore.ChatActions.TryRemove(BuildPendingChatActionKey(scope), out _);
                return new TelegramRouteResponse(
                    "nav.main.chat",
                    _navigationPresenter.GetText("menu.chat.title", locale),
                    ReplyKeyboard(_navigationPresenter.BuildMainReplyKeyboard(locale)));

            case NavigationRouteKind.ChatText:
                {
                    var pendingChatKey = BuildPendingChatActionKey(scope);
                    if (_pendingStateStore.ChatActions.TryGetValue(pendingChatKey, out var pendingAction))
                    {
                        var pendingResponse = await TryHandlePendingChatActionAsync(
                            pendingAction,
                            inbound,
                            scope,
                            locale,
                            cancellationToken);
                        if (pendingResponse is not null)
                        {
                            return pendingResponse;
                        }
                    }

                    if (TryResolveDeterministicChatActionIntent(inbound.Text, out var deterministicIntent))
                    {
                        var deterministicResponse = await TryHandleAssistantChatActionIntentAsync(
                            deterministicIntent,
                            inbound,
                            scope,
                            locale,
                            cancellationToken);
                        if (deterministicResponse is not null)
                        {
                            return deterministicResponse;
                        }
                    }

                    var chatInput = inbound.Text;
                    if (!chatInput.StartsWith("/", StringComparison.Ordinal))
                    {
                        chatInput = $"{ConversationInputMarkers.Chat} {chatInput}";
                    }

                    var result = await _orchestrator.ProcessAsync(
                        chatInput,
                        scope.Channel,
                        scope.UserId,
                        scope.ConversationId,
                        cancellationToken);

                    var actionResponse = await TryHandleAssistantChatActionIntentAsync(
                        result.Intent,
                        inbound,
                        scope,
                        locale,
                        cancellationToken);
                    if (actionResponse is not null)
                    {
                        return actionResponse;
                    }

                    var mainKeyboardLocale = await ResolveChatMainKeyboardLocaleAsync(
                        scope,
                        locale,
                        result.Intent,
                        cancellationToken);

                    return new TelegramRouteResponse(
                        result.Intent,
                        _responseFormatter.Format(result),
                        ReplyKeyboard(_navigationPresenter.BuildMainReplyKeyboard(mainKeyboardLocale)));
                }

            case NavigationRouteKind.MainVocabularyButton:
                await _navigationStateService.SetCurrentSectionAsync(scope.Channel, scope.UserId, scope.ConversationId, NavigationSections.Vocabulary, cancellationToken);
                return await BuildVocabularySectionResponseAsync(locale, cancellationToken);

            case NavigationRouteKind.MainShoppingButton:
                await _navigationStateService.SetCurrentSectionAsync(scope.Channel, scope.UserId, scope.ConversationId, NavigationSections.Shopping, cancellationToken);
                return new TelegramRouteResponse(
                    "nav.shopping",
                    _navigationPresenter.GetText("menu.shopping.title", locale),
                    InlineKeyboard(_navigationPresenter.BuildShoppingKeyboard(locale)));

            case NavigationRouteKind.MainWeeklyMenuButton:
                await _navigationStateService.SetCurrentSectionAsync(scope.Channel, scope.UserId, scope.ConversationId, NavigationSections.WeeklyMenu, cancellationToken);
                return new TelegramRouteResponse(
                    "nav.weekly",
                    _navigationPresenter.GetText("menu.weekly.title", locale),
                    InlineKeyboard(_navigationPresenter.BuildWeeklyMenuKeyboard(locale)));

            case NavigationRouteKind.MainSettingsButton:
                await _navigationStateService.SetCurrentSectionAsync(scope.Channel, scope.UserId, scope.ConversationId, NavigationSections.Settings, cancellationToken);
                return await BuildSettingsSectionResponseAsync(scope, locale, cancellationToken);

            case NavigationRouteKind.Callback:
                return await HandleCallbackAsync(route.CallbackData!, inbound, scope, locale, currentSection, cancellationToken);

            case NavigationRouteKind.VocabularyText:
                {
                    var importFlowResponse = await TryHandleVocabularyImportFlowAsync(
                        inbound,
                        scope,
                        locale,
                        cancellationToken);
                    if (importFlowResponse is not null)
                    {
                        return importFlowResponse;
                    }

                    var result = await _orchestrator.ProcessAsync(
                        inbound.Text,
                        scope.Channel,
                        scope.UserId,
                        scope.ConversationId,
                        cancellationToken);
                    return await BuildVocabularyTextResponseAsync(result, scope, locale, inbound.Text, cancellationToken);
                }

            case NavigationRouteKind.ShoppingText:
                return await HandleShoppingTextAsync(inbound.Text, locale, cancellationToken);

            case NavigationRouteKind.WeeklyMenuText:
                if (!string.IsNullOrWhiteSpace(inbound.PhotoFileId))
                    return await HandleWeeklyMenuPhotoAsync(inbound.PhotoFileId, locale, scope, cancellationToken);
                return await HandleWeeklyMenuTextAsync(inbound.Text, locale, scope, cancellationToken);

            case NavigationRouteKind.SettingsText:
                await _navigationStateService.SetCurrentSectionAsync(scope.Channel, scope.UserId, scope.ConversationId, NavigationSections.Settings, cancellationToken);
                return await BuildSettingsSectionResponseAsync(scope, locale, cancellationToken);

            case NavigationRouteKind.LanguageOnboardingText:
                await _navigationStateService.SetCurrentSectionAsync(scope.Channel, scope.UserId, scope.ConversationId, NavigationSections.LanguageOnboarding, cancellationToken);
                return BuildOnboardingLanguagePickerResponse(locale);

            case NavigationRouteKind.DefaultText:
            default:
                {
                    var completion = await _assistantSessionService.AskAsync(inbound.Text, cancellationToken);
                    return new TelegramRouteResponse("assistant.main", completion.Content);
                }
        }
    }

    private async Task<TelegramRouteResponse?> TryHandlePendingChatActionAsync(
        PendingChatActionKind action,
        TelegramInboundMessage inbound,
        ConversationScope scope,
        string locale,
        CancellationToken cancellationToken)
    {
        var pendingKey = BuildPendingChatActionKey(scope);
        switch (action)
        {
            case PendingChatActionKind.VocabularyImport:
                {
                    var importResponse = await TryHandleVocabularyImportFlowAsync(
                        inbound,
                        scope,
                        locale,
                        cancellationToken);

                    if (importResponse is null)
                    {
                        _pendingStateStore.ChatActions.TryRemove(pendingKey, out _);
                        return null;
                    }

                    var keepPending = _pendingStateStore.VocabularyUrlSessions.ContainsKey(BuildPendingUrlSessionKey(scope));
                    if (!keepPending)
                    {
                        _pendingStateStore.ChatActions.TryRemove(pendingKey, out _);
                    }

                    return await FinalizeChatActionResponseAsync(scope, importResponse, cancellationToken);
                }

            case PendingChatActionKind.VocabularyAdd:
            case PendingChatActionKind.VocabularyBatch:
                {
                    var text = inbound.Text?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        return null;
                    }

                    if (string.Equals(text, "/cancel", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(text, "cancel", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(text, "скасувати", StringComparison.OrdinalIgnoreCase))
                    {
                        _pendingStateStore.ChatActions.TryRemove(pendingKey, out _);
                        return await FinalizeChatActionResponseAsync(
                            scope,
                            new TelegramRouteResponse(
                                "assistant.chat.action.cancelled",
                                _navigationPresenter.GetText("vocab.url.selection_cancelled", locale),
                                ReplyKeyboard(_navigationPresenter.BuildMainReplyKeyboard(locale))),
                            cancellationToken);
                    }

                    var result = await _orchestrator.ProcessAsync(
                        text,
                        scope.Channel,
                        scope.UserId,
                        scope.ConversationId,
                        cancellationToken);

                    var response = await BuildVocabularyTextResponseAsync(result, scope, locale, text, cancellationToken);
                    _pendingStateStore.ChatActions.TryRemove(pendingKey, out _);
                    return await FinalizeChatActionResponseAsync(scope, response, cancellationToken);
                }

            default:
                _pendingStateStore.ChatActions.TryRemove(pendingKey, out _);
                return null;
        }
    }

    private async Task<TelegramRouteResponse?> TryHandleAssistantChatActionIntentAsync(
        string intent,
        TelegramInboundMessage inbound,
        ConversationScope scope,
        string locale,
        CancellationToken cancellationToken)
    {
        var pendingKey = BuildPendingChatActionKey(scope);

        switch (intent)
        {
            case "assistant.vocabulary.add.start":
                if (TryExtractInlineVocabularyWord(inbound.Text, out var inlineWord))
                {
                    var inlineResult = await _orchestrator.ProcessAsync(
                        inlineWord,
                        scope.Channel,
                        scope.UserId,
                        scope.ConversationId,
                        cancellationToken);

                    var inlineResponse = await BuildVocabularyTextResponseAsync(
                        inlineResult,
                        scope,
                        locale,
                        inlineWord,
                        cancellationToken);

                    _pendingStateStore.ChatActions.TryRemove(pendingKey, out _);
                    return await FinalizeChatActionResponseAsync(scope, inlineResponse, cancellationToken);
                }

                _pendingStateStore.ChatActions[pendingKey] = PendingChatActionKind.VocabularyAdd;
                return await FinalizeChatActionResponseAsync(
                    scope,
                    HandleVocabularyAddCallback(scope, locale),
                    cancellationToken);

            case "assistant.vocabulary.batch.start":
                _pendingStateStore.ChatActions[pendingKey] = PendingChatActionKind.VocabularyBatch;
                return await FinalizeChatActionResponseAsync(
                    scope,
                    BuildBatchModeResponse(scope, locale),
                    cancellationToken);

            case "assistant.vocabulary.import.start":
                _pendingStateStore.ChatActions[pendingKey] = PendingChatActionKind.VocabularyImport;
                return await FinalizeChatActionResponseAsync(
                    scope,
                    HandleVocabularyImportStartCallback(scope, locale),
                    cancellationToken);

            case "assistant.vocabulary.import.source.photo":
                _pendingStateStore.ChatActions[pendingKey] = PendingChatActionKind.VocabularyImport;
                return await FinalizeChatActionResponseAsync(
                    scope,
                    HandleVocabularyImportSourceCallback(scope, locale, TelegramImportSourceType.Photo),
                    cancellationToken);

            case "assistant.vocabulary.import.source.file":
                _pendingStateStore.ChatActions[pendingKey] = PendingChatActionKind.VocabularyImport;
                return await FinalizeChatActionResponseAsync(
                    scope,
                    HandleVocabularyImportSourceCallback(scope, locale, TelegramImportSourceType.File),
                    cancellationToken);

            case "assistant.vocabulary.import.source.url":
                _pendingStateStore.ChatActions[pendingKey] = PendingChatActionKind.VocabularyImport;
                return await FinalizeChatActionResponseAsync(
                    scope,
                    HandleVocabularyImportSourceCallback(scope, locale, TelegramImportSourceType.Url),
                    cancellationToken);

            case "assistant.vocabulary.import.source.text":
                _pendingStateStore.ChatActions[pendingKey] = PendingChatActionKind.VocabularyImport;
                return await FinalizeChatActionResponseAsync(
                    scope,
                    HandleVocabularyImportSourceCallback(scope, locale, TelegramImportSourceType.Text),
                    cancellationToken);

            case "assistant.vocabulary.stats":
                return await FinalizeChatActionResponseAsync(
                    scope,
                    await HandleVocabularyStatsCallbackAsync(locale, cancellationToken),
                    cancellationToken);

            case "assistant.vocabulary.open":
                return await FinalizeChatActionResponseAsync(
                    scope,
                    await BuildVocabularySectionResponseAsync(locale, cancellationToken),
                    cancellationToken);

            case "assistant.settings.open":
                return await FinalizeChatActionResponseAsync(
                    scope,
                    await BuildSettingsSectionResponseAsync(scope, locale, cancellationToken),
                    cancellationToken);

            case "assistant.settings.save_mode.open":
                return await FinalizeChatActionResponseAsync(
                    scope,
                    await BuildSaveModeResponseAsync(scope, locale, cancellationToken),
                    cancellationToken);

            case "assistant.settings.language.open":
                return await FinalizeChatActionResponseAsync(
                    scope,
                    await HandleSettingsCallbackAsync(CallbackDataConstants.Settings.Language, scope, locale, cancellationToken),
                    cancellationToken);

            case "assistant.settings.notion.open":
                return await FinalizeChatActionResponseAsync(
                    scope,
                    await HandleSettingsCallbackAsync(CallbackDataConstants.Settings.Notion, scope, locale, cancellationToken),
                    cancellationToken);

            case "assistant.onedrive.open":
            case "assistant.onedrive.status":
                return await FinalizeChatActionResponseAsync(
                    scope,
                    await BuildOneDriveResponseAsync(scope, locale, includeCheckStatusButton: false, cancellationToken),
                    cancellationToken);

            case "assistant.onedrive.login":
                return await FinalizeChatActionResponseAsync(
                    scope,
                    await HandleOneDriveCallbackAsync(CallbackDataConstants.OneDrive.Login, inbound, scope, locale, cancellationToken),
                    cancellationToken);

            case "assistant.onedrive.logout":
                return await FinalizeChatActionResponseAsync(
                    scope,
                    await HandleOneDriveCallbackAsync(CallbackDataConstants.OneDrive.Logout, inbound, scope, locale, cancellationToken),
                    cancellationToken);

            case "assistant.onedrive.sync":
                return await FinalizeChatActionResponseAsync(
                    scope,
                    await HandleOneDriveCallbackAsync(CallbackDataConstants.OneDrive.SyncNow, inbound, scope, locale, cancellationToken),
                    cancellationToken);

            case "assistant.onedrive.index.rebuild":
                return await FinalizeChatActionResponseAsync(
                    scope,
                    await HandleOneDriveCallbackAsync(CallbackDataConstants.OneDrive.RebuildIndex, inbound, scope, locale, cancellationToken),
                    cancellationToken);

            case "assistant.onedrive.cache.clear":
                return await FinalizeChatActionResponseAsync(
                    scope,
                    await HandleOneDriveCallbackAsync(CallbackDataConstants.OneDrive.ClearCache, inbound, scope, locale, cancellationToken),
                    cancellationToken);

            case "assistant.shopping.open":
                return await FinalizeChatActionResponseAsync(
                    scope,
                    new TelegramRouteResponse(
                        "nav.shopping",
                        _navigationPresenter.GetText("menu.shopping.title", locale),
                        InlineKeyboard(_navigationPresenter.BuildShoppingKeyboard(locale))),
                    cancellationToken);

            case "assistant.weekly.open":
                return await FinalizeChatActionResponseAsync(
                    scope,
                    new TelegramRouteResponse(
                        "nav.weekly",
                        _navigationPresenter.GetText("menu.weekly.title", locale),
                        InlineKeyboard(_navigationPresenter.BuildWeeklyMenuKeyboard(locale))),
                    cancellationToken);

            default:
                return null;
        }
    }

    private static bool TryExtractInlineVocabularyWord(string? rawInput, out string word)
    {
        word = string.Empty;
        if (string.IsNullOrWhiteSpace(rawInput))
        {
            return false;
        }

        var input = rawInput.Trim();
        var prefixes = new[]
        {
            "додай слово",
            "додати слово",
            "add word",
            "add new word"
        };

        var matchedPrefix = prefixes.FirstOrDefault(prefix =>
            input.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        if (matchedPrefix is null)
        {
            return false;
        }

        var remainder = input[matchedPrefix.Length..].Trim();
        if (string.IsNullOrWhiteSpace(remainder))
        {
            return false;
        }

        remainder = TrimLeadingWordSeparators(remainder);

        var dictionarySuffixes = new[]
        {
            "у словник",
            "в словник",
            "to dictionary"
        };

        foreach (var suffix in dictionarySuffixes)
        {
            if (!remainder.StartsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            remainder = remainder[suffix.Length..].Trim();
            remainder = TrimLeadingWordSeparators(remainder);
            break;
        }

        if (string.IsNullOrWhiteSpace(remainder))
        {
            return false;
        }

        word = remainder;
        return true;
    }

    private static bool TryResolveDeterministicChatActionIntent(string? rawInput, out string intent)
    {
        intent = string.Empty;
        if (string.IsNullOrWhiteSpace(rawInput))
        {
            return false;
        }

        var input = rawInput.Trim();
        if (input.StartsWith("/", StringComparison.Ordinal))
        {
            return false;
        }

        var lowered = input.ToLowerInvariant();
        var mentionsImport = lowered.Contains("імпорт", StringComparison.Ordinal)
            || lowered.Contains("імпорту", StringComparison.Ordinal)
            || lowered.Contains("import", StringComparison.Ordinal);
        var mentionsSend = lowered.Contains("скину", StringComparison.Ordinal)
            || lowered.Contains("надішлю", StringComparison.Ordinal)
            || lowered.Contains("send", StringComparison.Ordinal)
            || lowered.Contains("upload", StringComparison.Ordinal)
            || lowered.Contains("share", StringComparison.Ordinal);
        var mentionsLink = lowered.Contains("посилан", StringComparison.Ordinal)
            || lowered.Contains("лінк", StringComparison.Ordinal)
            || lowered.Contains("link", StringComparison.Ordinal)
            || lowered.Contains("url", StringComparison.Ordinal)
            || UrlLikeRegex.IsMatch(input);
        var mentionsPhoto = lowered.Contains("фото", StringComparison.Ordinal)
            || lowered.Contains("photo", StringComparison.Ordinal)
            || lowered.Contains("image", StringComparison.Ordinal)
            || lowered.Contains("picture", StringComparison.Ordinal);
        var mentionsFile = lowered.Contains("файл", StringComparison.Ordinal)
            || lowered.Contains("file", StringComparison.Ordinal)
            || lowered.Contains("pdf", StringComparison.Ordinal)
            || lowered.Contains("excel", StringComparison.Ordinal)
            || lowered.Contains("xlsx", StringComparison.Ordinal)
            || lowered.Contains("doc", StringComparison.Ordinal);
        var mentionsText = lowered.Contains("текст", StringComparison.Ordinal)
            || lowered.Contains("text", StringComparison.Ordinal)
            || lowered.Contains("статт", StringComparison.Ordinal)
            || lowered.Contains("article", StringComparison.Ordinal);

        if (!mentionsImport && !mentionsSend)
        {
            return false;
        }

        if (mentionsLink)
        {
            intent = "assistant.vocabulary.import.source.url";
            return true;
        }

        if (mentionsPhoto)
        {
            intent = "assistant.vocabulary.import.source.photo";
            return true;
        }

        if (mentionsFile)
        {
            intent = "assistant.vocabulary.import.source.file";
            return true;
        }

        if (mentionsText)
        {
            intent = "assistant.vocabulary.import.source.text";
            return true;
        }

        if (mentionsImport)
        {
            intent = "assistant.vocabulary.import.start";
            return true;
        }

        return false;
    }

    private static string TrimLeadingWordSeparators(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return LeadingWordSeparatorRegex.Replace(value.Trim(), string.Empty).Trim();
    }

    private async Task<TelegramRouteResponse> FinalizeChatActionResponseAsync(
        ConversationScope scope,
        TelegramRouteResponse response,
        CancellationToken cancellationToken)
    {
        await _navigationStateService.SetCurrentSectionAsync(
            scope.Channel,
            scope.UserId,
            scope.ConversationId,
            NavigationSections.Chat,
            cancellationToken);

        return response;
    }

    private async Task<string> ResolveChatMainKeyboardLocaleAsync(
        ConversationScope scope,
        string locale,
        string intent,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(intent, "assistant.settings.language.updated", StringComparison.Ordinal))
        {
            return locale;
        }

        var storedLocale = await _userLocaleStateService.GetStoredLocaleAsync(
            scope.Channel,
            scope.UserId,
            cancellationToken);
        return string.IsNullOrWhiteSpace(storedLocale)
            ? locale
            : LocalizationConstants.NormalizeLocaleCode(storedLocale);
    }

    private async Task<TelegramRouteResponse> HandleCallbackAsync(
        string callbackData,
        TelegramInboundMessage inbound,
        ConversationScope scope,
        string locale,
        string currentSection,
        CancellationToken cancellationToken)
    {
        if (string.Equals(callbackData, CallbackDataConstants.Nav.Main, StringComparison.Ordinal))
        {
            await _navigationStateService.SetCurrentSectionAsync(scope.Channel, scope.UserId, scope.ConversationId, NavigationSections.Main, cancellationToken);
            _pendingStateStore.VocabularyUrlSessions.TryRemove(BuildPendingUrlSessionKey(scope), out _);
            _pendingStateStore.ChatActions.TryRemove(BuildPendingChatActionKey(scope), out _);
            return new TelegramRouteResponse(
                "nav.main",
                _navigationPresenter.GetText("menu.main.title", locale),
                ReplyKeyboard(_navigationPresenter.BuildMainReplyKeyboard(locale)));
        }

        if (callbackData.StartsWith(CallbackDataConstants.Lang.Prefix, StringComparison.Ordinal))
        {
            return await HandleLanguageCallbackAsync(callbackData, scope, locale, currentSection, cancellationToken);
        }

        if (callbackData.StartsWith(CallbackDataConstants.Settings.Prefix, StringComparison.Ordinal))
        {
            return await HandleSettingsCallbackAsync(callbackData, scope, locale, cancellationToken);
        }

        if (callbackData.StartsWith(CallbackDataConstants.SaveMode.Prefix, StringComparison.Ordinal))
        {
            return await HandleSaveModeCallbackAsync(callbackData, scope, locale, cancellationToken);
        }

        if (callbackData.StartsWith(CallbackDataConstants.OneDrive.Prefix, StringComparison.Ordinal))
        {
            return await HandleOneDriveCallbackAsync(callbackData, inbound, scope, locale, cancellationToken);
        }

        if (callbackData.StartsWith(CallbackDataConstants.Vocab.Prefix, StringComparison.Ordinal))
        {
            await _navigationStateService.SetCurrentSectionAsync(scope.Channel, scope.UserId, scope.ConversationId, NavigationSections.Vocabulary, cancellationToken);

            return callbackData switch
            {
                CallbackDataConstants.Vocab.Add => HandleVocabularyAddCallback(scope, locale),
                CallbackDataConstants.Vocab.Stats or CallbackDataConstants.Vocab.ListLegacy
                    => await HandleVocabularyStatsCallbackAsync(locale, cancellationToken),
                CallbackDataConstants.Vocab.Url => HandleVocabularyImportStartCallback(scope, locale),
                CallbackDataConstants.Vocab.ImportSourcePhoto => HandleVocabularyImportSourceCallback(scope, locale, TelegramImportSourceType.Photo),
                CallbackDataConstants.Vocab.ImportSourceFile => HandleVocabularyImportSourceCallback(scope, locale, TelegramImportSourceType.File),
                CallbackDataConstants.Vocab.ImportSourceUrl => HandleVocabularyImportSourceCallback(scope, locale, TelegramImportSourceType.Url),
                CallbackDataConstants.Vocab.ImportSourceText => HandleVocabularyImportSourceCallback(scope, locale, TelegramImportSourceType.Text),
                CallbackDataConstants.Vocab.UrlSelectAll => await HandleVocabularyUrlSelectAllAsync(scope, locale, cancellationToken),
                CallbackDataConstants.Vocab.UrlCancel => HandleVocabularyUrlCancelCallback(scope, locale),
                CallbackDataConstants.Vocab.Batch => BuildBatchModeResponse(scope, locale),
                CallbackDataConstants.Vocab.SaveYes => await HandleVocabularySaveConfirmationAsync(scope, locale, saveRequested: true, cancellationToken),
                CallbackDataConstants.Vocab.SaveNo => await HandleVocabularySaveConfirmationAsync(scope, locale, saveRequested: false, cancellationToken),
                CallbackDataConstants.Vocab.SaveBatchYes => await HandleVocabularyBatchSaveConfirmationAsync(scope, locale, saveRequested: true, cancellationToken),
                CallbackDataConstants.Vocab.SaveBatchNo => await HandleVocabularyBatchSaveConfirmationAsync(scope, locale, saveRequested: false, cancellationToken),
                _ => new TelegramRouteResponse(
                    "vocab.unknown",
                    _navigationPresenter.GetText("stub.wip", locale),
                    InlineKeyboard(_navigationPresenter.BuildVocabularyKeyboard(locale)))
            };
        }

        if (callbackData.StartsWith(CallbackDataConstants.Shop.Prefix, StringComparison.Ordinal))
        {
            await _navigationStateService.SetCurrentSectionAsync(scope.Channel, scope.UserId, scope.ConversationId, NavigationSections.Shopping, cancellationToken);
            return await HandleFoodCallbackAsync(callbackData, locale, scope, cancellationToken);
        }

        if (callbackData.StartsWith(CallbackDataConstants.Weekly.Prefix, StringComparison.Ordinal))
        {
            await _navigationStateService.SetCurrentSectionAsync(scope.Channel, scope.UserId, scope.ConversationId, NavigationSections.WeeklyMenu, cancellationToken);
            return await HandleFoodCallbackAsync(callbackData, locale, scope, cancellationToken);
        }

        return new TelegramRouteResponse("nav.unknown", _navigationPresenter.GetText("stub.wip", locale));
    }

    private async Task<TelegramRouteResponse> HandleLanguageCallbackAsync(
        string callbackData,
        ConversationScope scope,
        string locale,
        string currentSection,
        CancellationToken cancellationToken)
    {
        if (string.Equals(callbackData, CallbackDataConstants.Lang.GermanPolish, StringComparison.Ordinal))
        {
            var section = NavigationSections.Normalize(currentSection);
            return section == NavigationSections.LanguageOnboarding
                ? new TelegramRouteResponse(
                    "onboarding.language.secondary",
                    BuildBilingualOnboardingText(),
                    InlineKeyboard(_navigationPresenter.BuildOnboardingSecondaryLanguageKeyboard(locale)))
                : new TelegramRouteResponse(
                    "settings.language.secondary",
                    _navigationPresenter.GetText("language.current", locale, _navigationPresenter.GetLanguageDisplayName(locale)),
                    InlineKeyboard(_navigationPresenter.BuildSettingsSecondaryLanguageKeyboard(locale)),
                    IsHtml: true);
        }

        if (string.Equals(callbackData, CallbackDataConstants.Lang.BackOnboarding, StringComparison.Ordinal))
        {
            await _navigationStateService.SetCurrentSectionAsync(scope.Channel, scope.UserId, scope.ConversationId, NavigationSections.LanguageOnboarding, cancellationToken);
            return BuildOnboardingLanguagePickerResponse(locale);
        }

        var selectedLocale = ParseLanguageCallback(callbackData);
        if (selectedLocale is null)
        {
            return new TelegramRouteResponse("language.invalid", _navigationPresenter.GetText("stub.wip", locale));
        }

        var newLocale = await _userLocaleStateService.SetLocaleAsync(
            scope.Channel,
            scope.UserId,
            selectedLocale,
            selectedManually: true,
            cancellationToken);

        var normalizedSection = NavigationSections.Normalize(currentSection);
        if (normalizedSection == NavigationSections.LanguageOnboarding)
        {
            await _navigationStateService.SetCurrentSectionAsync(scope.Channel, scope.UserId, scope.ConversationId, NavigationSections.Main, cancellationToken);
            return new TelegramRouteResponse(
                "onboarding.language.selected",
                _navigationPresenter.GetText("onboarding.language_saved", newLocale),
                ReplyKeyboard(_navigationPresenter.BuildMainReplyKeyboard(newLocale)));
        }

        await _navigationStateService.SetCurrentSectionAsync(scope.Channel, scope.UserId, scope.ConversationId, NavigationSections.Settings, cancellationToken);

        var changedText = _navigationPresenter.GetText("language.changed", newLocale, _navigationPresenter.GetLanguageDisplayName(newLocale));
        var settingsScreen = await BuildSettingsSectionResponseAsync(scope, newLocale, cancellationToken);

        return settingsScreen with
        {
            Intent = "settings.language.changed",
            Text = string.Concat(changedText, Environment.NewLine, Environment.NewLine, settingsScreen.Text),
            FollowUpMainKeyboardLocale = newLocale
        };
    }

    private async Task<TelegramRouteResponse> HandleSettingsCallbackAsync(
        string callbackData,
        ConversationScope scope,
        string locale,
        CancellationToken cancellationToken)
    {
        await _navigationStateService.SetCurrentSectionAsync(scope.Channel, scope.UserId, scope.ConversationId, NavigationSections.Settings, cancellationToken);

        return callbackData switch
        {
            CallbackDataConstants.Settings.Language => new TelegramRouteResponse(
                "settings.language",
                _navigationPresenter.GetText("language.current", locale, _navigationPresenter.GetLanguageDisplayName(locale)),
                InlineKeyboard(_navigationPresenter.BuildSettingsLanguageKeyboard(locale)),
                IsHtml: true),
            CallbackDataConstants.Settings.SaveMode => await BuildSaveModeResponseAsync(scope, locale, cancellationToken),
            CallbackDataConstants.Settings.OneDrive => await BuildOneDriveResponseAsync(scope, locale, includeCheckStatusButton: false, cancellationToken),
            CallbackDataConstants.Settings.Notion => BuildNotionSectionResponse(locale),
            CallbackDataConstants.Settings.Back => await BuildSettingsSectionResponseAsync(scope, locale, cancellationToken),
            _ => await BuildSettingsSectionResponseAsync(scope, locale, cancellationToken)
        };
    }

    private async Task<TelegramRouteResponse> HandleSaveModeCallbackAsync(
        string callbackData,
        ConversationScope scope,
        string locale,
        CancellationToken cancellationToken)
    {
        if (!callbackData.StartsWith(CallbackDataConstants.SaveMode.Prefix, StringComparison.Ordinal))
        {
            return await BuildSettingsSectionResponseAsync(scope, locale, cancellationToken);
        }

        var requestedMode = callbackData[CallbackDataConstants.SaveMode.Prefix.Length..].Trim();
        if (!_saveModePreferenceService.TryParse(requestedMode, out var saveMode))
        {
            return await BuildSaveModeResponseAsync(scope, locale, cancellationToken);
        }

        await _saveModePreferenceService.SetModeAsync(scope, saveMode, cancellationToken);

        var changed = _navigationPresenter.GetText(
            "savemode.changed",
            locale,
            _saveModePreferenceService.ToText(saveMode));

        var settings = await BuildSettingsSectionResponseAsync(scope, locale, cancellationToken);
        return settings with
        {
            Intent = "settings.savemode.changed",
            Text = string.Concat(changed, Environment.NewLine, Environment.NewLine, settings.Text),
            IsHtml = true
        };
    }

    private async Task<TelegramRouteResponse> HandleOneDriveCallbackAsync(
        string callbackData,
        TelegramInboundMessage inbound,
        ConversationScope scope,
        string locale,
        CancellationToken cancellationToken)
    {
        if (string.Equals(callbackData, CallbackDataConstants.OneDrive.Logout, StringComparison.Ordinal))
        {
            await _graphAuthService.LogoutAsync(cancellationToken);
            _pendingStateStore.GraphChallenges.TryRemove(BuildGraphChallengeKey(scope), out _);

            var screen = await BuildOneDriveResponseAsync(scope, locale, includeCheckStatusButton: false, cancellationToken);
            return screen with
            {
                Intent = "settings.onedrive.logout",
                Text = string.Concat(
                    _navigationPresenter.GetText("onedrive.logout_done", locale),
                    Environment.NewLine,
                    Environment.NewLine,
                    screen.Text)
            };
        }

        if (string.Equals(callbackData, CallbackDataConstants.OneDrive.Login, StringComparison.Ordinal))
        {
            var start = await _graphAuthService.StartLoginAsync(cancellationToken);
            if (!start.Succeeded || start.Challenge is null)
            {
                var screen = await BuildOneDriveResponseAsync(scope, locale, includeCheckStatusButton: false, cancellationToken);
                return screen with
                {
                    Intent = "settings.onedrive.login.failed",
                    Text = string.Concat(
                        WebUtility.HtmlEncode(start.Message),
                        Environment.NewLine,
                        Environment.NewLine,
                        screen.Text)
                };
            }

            _pendingStateStore.GraphChallenges[BuildGraphChallengeKey(scope)] = start.Challenge;
            var expiresInMinutes = Math.Max(1, (int)Math.Ceiling(start.Challenge.ExpiresInSeconds / 60d));

            return new TelegramRouteResponse(
                "settings.onedrive.login.started",
                _navigationPresenter.GetText(
                    "onedrive.login_started",
                    locale,
                    WebUtility.HtmlEncode(start.Challenge.UserCode),
                    WebUtility.HtmlEncode(start.Challenge.VerificationUri),
                    expiresInMinutes),
                InlineKeyboard(_navigationPresenter.BuildOneDriveKeyboard(locale, isConnected: false, includeCheckStatusButton: true)),
                IsHtml: true);
        }

        if (string.Equals(callbackData, CallbackDataConstants.OneDrive.CheckLogin, StringComparison.Ordinal))
        {
            var challengeKey = BuildGraphChallengeKey(scope);
            if (!_pendingStateStore.GraphChallenges.TryGetValue(challengeKey, out var challenge))
            {
                var missingScreen = await BuildOneDriveResponseAsync(scope, locale, includeCheckStatusButton: false, cancellationToken);
                return missingScreen with
                {
                    Intent = "settings.onedrive.check.missing",
                    Text = string.Concat(
                        EnsureQuestionMarker(_navigationPresenter.GetText("onedrive.still_not_signed_in", locale)),
                        Environment.NewLine,
                        Environment.NewLine,
                        missingScreen.Text)
                };
            }

            if (challenge.ExpiresAtUtc <= DateTimeOffset.UtcNow)
            {
                _pendingStateStore.GraphChallenges.TryRemove(challengeKey, out _);
                var expiredScreen = await BuildOneDriveResponseAsync(scope, locale, includeCheckStatusButton: false, cancellationToken);
                return expiredScreen with
                {
                    Intent = "settings.onedrive.check.expired",
                    Text = string.Concat(
                        EnsureQuestionMarker(_navigationPresenter.GetText("onedrive.still_not_signed_in", locale)),
                        Environment.NewLine,
                        Environment.NewLine,
                        expiredScreen.Text)
                };
            }

            var complete = await _graphAuthService.CompleteLoginAsync(challenge, cancellationToken);
            OneDriveSyncSummary? syncSummary = null;
            if (complete.Succeeded)
            {
                _pendingStateStore.GraphChallenges.TryRemove(challengeKey, out _);
                await _storagePreferenceService.SetModeAsync(scope, VocabularyStorageMode.Graph, cancellationToken);
                _storageModeProvider.SetMode(VocabularyStorageMode.Graph);

                try
                {
                    syncSummary = await RunPendingSyncUntilStableAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Post-login vocabulary sync attempt failed.");
                }
            }

            var includeCheckButton = !complete.Succeeded;
            var screen = await BuildOneDriveResponseAsync(scope, locale, includeCheckStatusButton: includeCheckButton, cancellationToken);

            if (complete.Succeeded)
            {
                var successText = _navigationPresenter.GetText("onedrive.login_switched_to_graph", locale);
                if (syncSummary is not null)
                {
                    successText = AppendTextBlock(
                        successText,
                        _navigationPresenter.GetText(
                            "onedrive.sync_now_done",
                            locale,
                            syncSummary.Completed,
                            syncSummary.Requeued,
                            syncSummary.Failed,
                            syncSummary.PendingAfterRun));
                }

                var indexHint = await BuildPostLoginIndexHintAsync(syncSummary, locale, cancellationToken);
                if (!string.IsNullOrWhiteSpace(indexHint))
                {
                    successText = AppendTextBlock(
                        successText,
                        indexHint);
                }

                return screen with
                {
                    Intent = "settings.onedrive.check.success",
                    Text = string.Concat(
                        successText,
                        Environment.NewLine,
                        Environment.NewLine,
                        screen.Text)
                };
            }

            return screen with
            {
                Intent = "settings.onedrive.check.pending",
                Text = string.Concat(
                    EnsureQuestionMarker(_navigationPresenter.GetText("onedrive.still_not_signed_in", locale)),
                    Environment.NewLine,
                    Environment.NewLine,
                    WebUtility.HtmlEncode(LocalizeGraphRelatedMessage(complete.Message, locale)),
                    Environment.NewLine,
                    Environment.NewLine,
                    screen.Text)
            };
        }

        if (string.Equals(callbackData, CallbackDataConstants.OneDrive.SyncNow, StringComparison.Ordinal))
        {
            var status = await _graphAuthService.GetStatusAsync(cancellationToken);
            if (!status.IsAuthenticated)
            {
                var authScreen = await BuildOneDriveResponseAsync(scope, locale, includeCheckStatusButton: false, cancellationToken);
                return authScreen with
                {
                    Intent = "settings.onedrive.sync.auth_required",
                    Text = string.Concat(
                        _navigationPresenter.GetText("onedrive.error_not_authenticated", locale),
                        Environment.NewLine,
                        Environment.NewLine,
                        authScreen.Text)
                };
            }

            try
            {
                var syncSummary = await RunPendingSyncUntilStableAsync(cancellationToken);
                var syncedScreen = await BuildOneDriveResponseAsync(scope, locale, includeCheckStatusButton: false, cancellationToken);

                return syncedScreen with
                {
                    Intent = "settings.onedrive.sync.done",
                    Text = string.Concat(
                        _navigationPresenter.GetText(
                            "onedrive.sync_now_done",
                            locale,
                            syncSummary.Completed,
                            syncSummary.Requeued,
                            syncSummary.Failed,
                            syncSummary.PendingAfterRun),
                        Environment.NewLine,
                        Environment.NewLine,
                        syncedScreen.Text)
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Manual vocabulary sync from Telegram settings failed.");
                var failedScreen = await BuildOneDriveResponseAsync(scope, locale, includeCheckStatusButton: false, cancellationToken);
                return failedScreen with
                {
                    Intent = "settings.onedrive.sync.failed",
                    Text = string.Concat(
                        _navigationPresenter.GetText(
                            "onedrive.operation_failed",
                            locale,
                            WebUtility.HtmlEncode(LocalizeGraphRelatedMessage(ex.Message, locale))),
                        Environment.NewLine,
                        Environment.NewLine,
                        failedScreen.Text)
                };
            }
        }

        if (string.Equals(callbackData, CallbackDataConstants.OneDrive.RebuildIndex, StringComparison.Ordinal))
        {
            return new TelegramRouteResponse(
                "settings.onedrive.index.confirm",
                EnsureQuestionMarker(_navigationPresenter.GetText("onedrive.rebuild_index_warning", locale)),
                InlineKeyboard(_navigationPresenter.BuildOneDriveRebuildIndexConfirmationKeyboard(locale)));
        }

        if (string.Equals(callbackData, CallbackDataConstants.OneDrive.ClearCache, StringComparison.Ordinal))
        {
            var cachedCount = 0;
            try
            {
                cachedCount = await _vocabularyCardRepository.CountAllAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not count cached words before clear-cache confirmation.");
            }

            return new TelegramRouteResponse(
                "settings.onedrive.cache.confirm",
                EnsureQuestionMarker(_navigationPresenter.GetText("onedrive.clear_cache_warning", locale, cachedCount)),
                InlineKeyboard(_navigationPresenter.BuildOneDriveClearCacheConfirmationKeyboard(locale)));
        }

        if (string.Equals(callbackData, CallbackDataConstants.OneDrive.RebuildIndexConfirm, StringComparison.Ordinal))
        {
            var status = await _graphAuthService.GetStatusAsync(cancellationToken);
            if (!status.IsAuthenticated)
            {
                var authScreen = await BuildOneDriveResponseAsync(scope, locale, includeCheckStatusButton: false, cancellationToken);
                return authScreen with
                {
                    Intent = "settings.onedrive.index.auth_required",
                    Text = string.Concat(
                        _navigationPresenter.GetText("onedrive.error_not_authenticated", locale),
                        Environment.NewLine,
                        Environment.NewLine,
                        authScreen.Text)
                };
            }

            try
            {
                await TrySendProgressMessageAsync(
                    inbound.ChatId,
                    inbound.MessageThreadId,
                    _navigationPresenter.GetText("onedrive.rebuild_index_started", locale),
                    cancellationToken);

                _storageModeProvider.SetMode(VocabularyStorageMode.Graph);
                var entries = await _vocabularyDeckService.GetAllEntriesAsync(cancellationToken);
                var indexed = await _vocabularyIndexService.RebuildAsync(entries, VocabularyStorageMode.Graph, cancellationToken);

                var indexedScreen = await BuildOneDriveResponseAsync(scope, locale, includeCheckStatusButton: false, cancellationToken);
                return indexedScreen with
                {
                    Intent = "settings.onedrive.index.done",
                    Text = string.Concat(
                        _navigationPresenter.GetText("onedrive.rebuild_index_done", locale, entries.Count, indexed),
                        Environment.NewLine,
                        Environment.NewLine,
                        indexedScreen.Text)
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "OneDrive index rebuild from Telegram settings failed.");
                var failedScreen = await BuildOneDriveResponseAsync(scope, locale, includeCheckStatusButton: false, cancellationToken);
                return failedScreen with
                {
                    Intent = "settings.onedrive.index.failed",
                    Text = string.Concat(
                        _navigationPresenter.GetText(
                            "onedrive.operation_failed",
                            locale,
                            WebUtility.HtmlEncode(LocalizeGraphRelatedMessage(ex.Message, locale))),
                        Environment.NewLine,
                        Environment.NewLine,
                        failedScreen.Text)
                };
            }
        }

        if (string.Equals(callbackData, CallbackDataConstants.OneDrive.ClearCacheConfirm, StringComparison.Ordinal))
        {
            try
            {
                var deleted = await _vocabularyIndexService.ClearAsync(cancellationToken);
                var clearedScreen = await BuildOneDriveResponseAsync(scope, locale, includeCheckStatusButton: false, cancellationToken);

                return clearedScreen with
                {
                    Intent = "settings.onedrive.cache.done",
                    Text = string.Concat(
                        _navigationPresenter.GetText("onedrive.clear_cache_done", locale, deleted),
                        Environment.NewLine,
                        Environment.NewLine,
                        _navigationPresenter.GetText("onedrive.clear_cache_hint", locale),
                        Environment.NewLine,
                        Environment.NewLine,
                        clearedScreen.Text)
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "OneDrive cache clear from Telegram settings failed.");
                var failedScreen = await BuildOneDriveResponseAsync(scope, locale, includeCheckStatusButton: false, cancellationToken);
                return failedScreen with
                {
                    Intent = "settings.onedrive.cache.failed",
                    Text = string.Concat(
                        _navigationPresenter.GetText(
                            "onedrive.operation_failed",
                            locale,
                            WebUtility.HtmlEncode(LocalizeGraphRelatedMessage(ex.Message, locale))),
                        Environment.NewLine,
                        Environment.NewLine,
                        failedScreen.Text)
                };
            }
        }

        return await BuildOneDriveResponseAsync(scope, locale, includeCheckStatusButton: false, cancellationToken);
    }

    private async Task<string?> BuildPostLoginIndexHintAsync(
        OneDriveSyncSummary? syncSummary,
        string locale,
        CancellationToken cancellationToken)
    {
        if (syncSummary is null)
        {
            return null;
        }

        if (syncSummary.Completed > 0 || syncSummary.PendingAfterRun > 0)
        {
            return null;
        }

        try
        {
            var indexedCount = await _vocabularyCardRepository.CountAllAsync(cancellationToken);
            if (indexedCount == 0)
            {
                return _navigationPresenter.GetText("onedrive.rebuild_index_suggest", locale);
            }

            return _navigationPresenter.GetText("onedrive.index_ready", locale, indexedCount);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not evaluate whether index rebuild suggestion is needed.");
            return null;
        }
    }

    private async Task<OneDriveSyncSummary> RunPendingSyncUntilStableAsync(CancellationToken cancellationToken)
    {
        var completed = 0;
        var requeued = 0;
        var failed = 0;
        var pendingAfterRun = 0;

        for (var pass = 0; pass < ManualSyncMaxPasses; pass++)
        {
            var summary = await _vocabularySyncProcessor.ProcessPendingAsync(ManualSyncBatchSize, cancellationToken);
            completed += summary.Completed;
            requeued += summary.Requeued;
            failed += summary.Failed;
            pendingAfterRun = summary.PendingAfterRun;

            if (summary.Processed == 0 || summary.PendingAfterRun == 0)
            {
                break;
            }
        }

        return new OneDriveSyncSummary(completed, requeued, failed, pendingAfterRun);
    }

    private TelegramRouteResponse HandleVocabularyAddCallback(
        ConversationScope scope,
        string locale)
    {
        _pendingStateStore.VocabularyUrlSessions.TryRemove(BuildPendingUrlSessionKey(scope), out _);

        return new TelegramRouteResponse(
            "vocab.add",
            _navigationPresenter.GetText("vocab.add.prompt", locale),
            InlineKeyboard(_navigationPresenter.BuildVocabularyKeyboard(locale)));
    }

    private TelegramRouteResponse HandleVocabularyImportStartCallback(
        ConversationScope scope,
        string locale)
    {
        _pendingStateStore.VocabularyUrlSessions[BuildPendingUrlSessionKey(scope)] = PendingVocabularyUrlSession.AwaitingSourceType;

        return new TelegramRouteResponse(
            "vocab.import",
            _navigationPresenter.GetText("vocab.import.choose_source", locale),
            InlineKeyboard(_navigationPresenter.BuildVocabularyImportSourceKeyboard(locale)));
    }

    private TelegramRouteResponse HandleVocabularyImportSourceCallback(
        ConversationScope scope,
        string locale,
        TelegramImportSourceType sourceType)
    {
        _pendingStateStore.VocabularyUrlSessions[BuildPendingUrlSessionKey(scope)] = PendingVocabularyUrlSession.AwaitingSourceInput(sourceType);

        var promptKey = sourceType switch
        {
            TelegramImportSourceType.Photo => "vocab.import.prompt.photo",
            TelegramImportSourceType.File => "vocab.import.prompt.file",
            TelegramImportSourceType.Url => "vocab.import.prompt.url",
            _ => "vocab.import.prompt.text"
        };

        return new TelegramRouteResponse(
            "vocab.import.source",
            _navigationPresenter.GetText(promptKey, locale),
            InlineKeyboard(_navigationPresenter.BuildVocabularyImportSourceKeyboard(locale)));
    }

    private TelegramRouteResponse HandleVocabularyUrlCancelCallback(
        ConversationScope scope,
        string locale)
    {
        _pendingStateStore.VocabularyUrlSessions.TryRemove(BuildPendingUrlSessionKey(scope), out _);

        return new TelegramRouteResponse(
            "vocab.url.cancel",
            _navigationPresenter.GetText("vocab.url.selection_cancelled", locale),
            InlineKeyboard(_navigationPresenter.BuildVocabularyKeyboard(locale)));
    }

    private async Task<TelegramRouteResponse> HandleVocabularyUrlSelectAllAsync(
        ConversationScope scope,
        string locale,
        CancellationToken cancellationToken)
    {
        var key = BuildPendingUrlSessionKey(scope);
        if (!_pendingStateStore.VocabularyUrlSessions.TryGetValue(key, out var session)
            || session.Stage != PendingVocabularyUrlStage.AwaitingSelection
            || session.Candidates.Count == 0)
        {
            return new TelegramRouteResponse(
                "vocab.url.no_pending",
                _navigationPresenter.GetText("vocab.url.no_pending", locale),
                InlineKeyboard(_navigationPresenter.BuildVocabularyKeyboard(locale)));
        }

        var selectedWords = session.Candidates
            .Select(candidate => candidate.Word)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return await ProcessVocabularyUrlSelectionAsync(selectedWords, scope, locale, cancellationToken);
    }

    private async Task<TelegramRouteResponse?> TryHandleVocabularyImportFlowAsync(
        TelegramInboundMessage inbound,
        ConversationScope scope,
        string locale,
        CancellationToken cancellationToken)
    {
        var normalizedInput = inbound.Text?.Trim() ?? string.Empty;
        var hasAnyInput = !string.IsNullOrWhiteSpace(normalizedInput)
                          || !string.IsNullOrWhiteSpace(inbound.PhotoFileId)
                          || !string.IsNullOrWhiteSpace(inbound.DocumentFileId);
        if (!hasAnyInput)
        {
            return null;
        }

        var pendingKey = BuildPendingUrlSessionKey(scope);
        var hasSession = _pendingStateStore.VocabularyUrlSessions.TryGetValue(pendingKey, out var session);
        var autoDetectedSource = DetectSourceTypeFromInbound(inbound);
        var shouldAutoStart = !hasSession
                              && (UrlLikeRegex.IsMatch(normalizedInput)
                                  || !string.IsNullOrWhiteSpace(inbound.PhotoFileId)
                                  || !string.IsNullOrWhiteSpace(inbound.DocumentFileId));

        if (!hasSession && !shouldAutoStart)
        {
            return null;
        }

        session ??= PendingVocabularyUrlSession.AwaitingSourceInput(autoDetectedSource ?? TelegramImportSourceType.Url);

        if (session.Stage == PendingVocabularyUrlStage.AwaitingSourceType)
        {
            var autoDetected = DetectSourceTypeFromInbound(inbound);
            if (autoDetected is null || autoDetected == TelegramImportSourceType.Text)
            {
                _pendingStateStore.VocabularyUrlSessions.TryRemove(pendingKey, out _);
                return null;
            }

            session = PendingVocabularyUrlSession.AwaitingSourceInput(autoDetected.Value);
            _pendingStateStore.VocabularyUrlSessions[pendingKey] = session;
        }

        if (session.Stage == PendingVocabularyUrlStage.AwaitingSelection)
        {
            var selection = ParseVocabularyUrlSelection(normalizedInput, session.Candidates);
            if (selection.Action == VocabularyUrlSelectionAction.Cancel)
            {
                return HandleVocabularyUrlCancelCallback(scope, locale);
            }

            if (selection.Action == VocabularyUrlSelectionAction.Invalid)
            {
                if (LooksLikeUrlSelectionAttempt(normalizedInput))
                {
                    return new TelegramRouteResponse(
                        "vocab.url.selection.invalid",
                        _navigationPresenter.GetText("vocab.url.select_parse_failed", locale),
                        InlineKeyboard(_navigationPresenter.BuildVocabularyUrlSelectionKeyboard(locale)));
                }

                _pendingStateStore.VocabularyUrlSessions.TryRemove(pendingKey, out _);
                return null;
            }

            return await ProcessVocabularyUrlSelectionAsync(
                selection.SelectedWords,
                scope,
                locale,
                cancellationToken);
        }

        if (session.SourceType is null)
        {
            _pendingStateStore.VocabularyUrlSessions[pendingKey] = PendingVocabularyUrlSession.AwaitingSourceType;
            return new TelegramRouteResponse(
                "vocab.import",
                _navigationPresenter.GetText("vocab.import.choose_source", locale),
                InlineKeyboard(_navigationPresenter.BuildVocabularyImportSourceKeyboard(locale)));
        }

        var importInbound = new TelegramImportInbound(
            inbound.Text ?? string.Empty,
            inbound.DocumentFileId,
            inbound.DocumentFileName,
            inbound.DocumentMimeType,
            inbound.PhotoFileId);
        var read = await _importSourceReader.ReadTextAsync(importInbound, session.SourceType.Value, cancellationToken);
        if (read.Status != TelegramImportSourceReadStatus.Success || string.IsNullOrWhiteSpace(read.Text))
        {
            _pendingStateStore.VocabularyUrlSessions[pendingKey] = session;
            return BuildImportReadErrorResponse(read, session.SourceType.Value, locale);
        }

        var discovery = await _vocabularyDiscoveryService.DiscoverAsync(read.Text, cancellationToken);
        if (discovery.Status == VocabularyDiscoveryStatus.InvalidSource
            || discovery.Status == VocabularyDiscoveryStatus.Failed)
        {
            _pendingStateStore.VocabularyUrlSessions[pendingKey] = session;

            return new TelegramRouteResponse(
                "vocab.url.invalid",
                _navigationPresenter.GetText("vocab.url.invalid", locale),
                InlineKeyboard(_navigationPresenter.BuildVocabularyImportSourceKeyboard(locale)));
        }

        if (discovery.Status != VocabularyDiscoveryStatus.Success || discovery.Candidates.Count == 0)
        {
            _pendingStateStore.VocabularyUrlSessions.TryRemove(pendingKey, out _);
            return new TelegramRouteResponse(
                "vocab.url.empty",
                _navigationPresenter.GetText("vocab.url.empty", locale),
                InlineKeyboard(_navigationPresenter.BuildVocabularyKeyboard(locale)));
        }

        var orderedCandidates = OrderUrlCandidates(discovery.Candidates);
        if (orderedCandidates.Count == 1)
        {
            _pendingStateStore.VocabularyUrlSessions.TryRemove(pendingKey, out _);
            return await ProcessVocabularyUrlSelectionAsync(
                [orderedCandidates[0].Word],
                scope,
                locale,
                cancellationToken);
        }

        _pendingStateStore.VocabularyUrlSessions[pendingKey] = PendingVocabularyUrlSession.AwaitingSelection(orderedCandidates);

        return new TelegramRouteResponse(
            "vocab.url.suggestions",
            BuildVocabularyUrlSuggestionsMessage(orderedCandidates, locale),
            InlineKeyboard(_navigationPresenter.BuildVocabularyUrlSelectionKeyboard(locale)));
    }

    private async Task<TelegramRouteResponse> ProcessVocabularyUrlSelectionAsync(
        IReadOnlyList<string> selectedWords,
        ConversationScope scope,
        string locale,
        CancellationToken cancellationToken)
    {
        if (selectedWords.Count == 0)
        {
            return new TelegramRouteResponse(
                "vocab.url.selection.invalid",
                _navigationPresenter.GetText("vocab.url.select_parse_failed", locale),
                InlineKeyboard(_navigationPresenter.BuildVocabularyUrlSelectionKeyboard(locale)));
        }

        _pendingStateStore.VocabularyUrlSessions.TryRemove(BuildPendingUrlSessionKey(scope), out _);
        var batchInput = string.Join(Environment.NewLine, selectedWords);
        var result = await _orchestrator.ProcessAsync(
            batchInput,
            scope.Channel,
            scope.UserId,
            scope.ConversationId,
            cancellationToken);

        return await BuildVocabularyTextResponseAsync(result, scope, locale, batchInput, cancellationToken);
    }

    private static IReadOnlyList<PendingVocabularyUrlCandidate> OrderUrlCandidates(
        IReadOnlyList<VocabularyDiscoveryCandidate> candidates)
    {
        var ordered = candidates
            .Where(candidate => candidate.PartOfSpeech is "n" or "v" or "adj")
            .GroupBy(candidate => candidate.Word, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(candidate => candidate.Frequency)
                .First())
            .OrderBy(candidate => Array.IndexOf(UrlSelectionPosOrder, candidate.PartOfSpeech))
            .ThenByDescending(candidate => candidate.Frequency)
            .ThenBy(candidate => candidate.Word, StringComparer.Ordinal)
            .ToList();

        var numbered = new List<PendingVocabularyUrlCandidate>(ordered.Count);
        for (var index = 0; index < ordered.Count; index++)
        {
            var current = ordered[index];
            numbered.Add(new PendingVocabularyUrlCandidate(index + 1, current.Word, current.PartOfSpeech, current.Frequency));
        }

        return numbered;
    }

    private string BuildVocabularyUrlSuggestionsMessage(
        IReadOnlyList<PendingVocabularyUrlCandidate> candidates,
        string locale)
    {
        var lines = new List<string>
        {
            _navigationPresenter.GetText("vocab.url.suggestions_title", locale, candidates.Count)
        };

        foreach (var pos in UrlSelectionPosOrder)
        {
            var groupItems = candidates
                .Where(candidate => string.Equals(candidate.PartOfSpeech, pos, StringComparison.Ordinal))
                .ToList();

            if (groupItems.Count == 0)
            {
                continue;
            }

            lines.Add(string.Empty);
            lines.Add(_navigationPresenter.GetText($"vocab.url.suggestions_group_{pos}", locale));

            foreach (var item in groupItems)
            {
                lines.Add($"{BatchItemMarker} {item.Number}) {item.Word}");
            }
        }

        lines.Add(string.Empty);
        lines.Add(_navigationPresenter.GetText("vocab.url.suggestions_hint", locale));
        return string.Join(Environment.NewLine, lines);
    }

    private VocabularyUrlSelectionResult ParseVocabularyUrlSelection(
        string input,
        IReadOnlyList<PendingVocabularyUrlCandidate> candidates)
    {
        var normalized = input.Trim();
        if (UrlCancelTokens.Contains(normalized))
        {
            return VocabularyUrlSelectionResult.Cancelled;
        }

        if (UrlSelectAllTokens.Contains(normalized))
        {
            var allWords = candidates
                .Select(candidate => candidate.Word)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            return new VocabularyUrlSelectionResult(VocabularyUrlSelectionAction.Select, allWords);
        }

        var wordsByNumber = SelectionNumberRegex.Matches(normalized)
            .Select(match => int.TryParse(match.Value, out var number) ? number : 0)
            .Where(number => number > 0)
            .Distinct()
            .Select(number => candidates.FirstOrDefault(candidate => candidate.Number == number)?.Word)
            .Where(word => !string.IsNullOrWhiteSpace(word))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (wordsByNumber.Count > 0)
        {
            return new VocabularyUrlSelectionResult(VocabularyUrlSelectionAction.Select, wordsByNumber);
        }

        var wordsByToken = normalized
            .Split([',', ';', '\n', '\r', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(token => token.Trim().ToLowerInvariant())
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(token => candidates.FirstOrDefault(candidate =>
                string.Equals(candidate.Word, token, StringComparison.OrdinalIgnoreCase))?.Word)
            .Where(word => !string.IsNullOrWhiteSpace(word))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (wordsByToken.Count > 0)
        {
            return new VocabularyUrlSelectionResult(VocabularyUrlSelectionAction.Select, wordsByToken);
        }

        return VocabularyUrlSelectionResult.Invalid;
    }

    private static bool LooksLikeUrlSelectionAttempt(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var normalized = input.Trim();
        if (UrlSelectAllTokens.Contains(normalized) || UrlCancelTokens.Contains(normalized))
        {
            return true;
        }

        if (SelectionNumberRegex.IsMatch(normalized))
        {
            return true;
        }

        return normalized.Contains(',', StringComparison.Ordinal)
            || normalized.Contains(';', StringComparison.Ordinal);
    }

    private TelegramRouteResponse BuildImportReadErrorResponse(
        TelegramImportSourceReadResult readResult,
        TelegramImportSourceType sourceType,
        string locale)
    {
        var key = readResult.Status switch
        {
            TelegramImportSourceReadStatus.WrongInputType => sourceType switch
            {
                TelegramImportSourceType.Photo => "vocab.import.invalid_expected_photo",
                TelegramImportSourceType.File => "vocab.import.invalid_expected_file",
                TelegramImportSourceType.Url => "vocab.import.invalid_expected_url",
                _ => "vocab.import.invalid_expected_text"
            },
            TelegramImportSourceReadStatus.UnsupportedFileType => "vocab.import.file_unsupported",
            TelegramImportSourceReadStatus.NoTextExtracted => sourceType == TelegramImportSourceType.Photo
                ? "vocab.import.photo_no_text"
                : "vocab.import.file_no_text",
            TelegramImportSourceReadStatus.InvalidSource => sourceType == TelegramImportSourceType.Url
                ? "vocab.import.invalid_expected_url"
                : "vocab.import.invalid_expected_text",
            _ => "vocab.import.read_failed"
        };

        var text = key == "vocab.import.read_failed"
            ? _navigationPresenter.GetText(key, locale, readResult.Error ?? "unknown error")
            : _navigationPresenter.GetText(key, locale);

        return new TelegramRouteResponse(
            "vocab.import.invalid",
            text,
            InlineKeyboard(_navigationPresenter.BuildVocabularyImportSourceKeyboard(locale)));
    }

    private static TelegramImportSourceType? DetectSourceTypeFromInbound(TelegramInboundMessage inbound)
    {
        if (!string.IsNullOrWhiteSpace(inbound.PhotoFileId))
        {
            return TelegramImportSourceType.Photo;
        }

        if (!string.IsNullOrWhiteSpace(inbound.DocumentFileId))
        {
            return TelegramImportSourceType.File;
        }

        var text = inbound.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return UrlLikeRegex.IsMatch(text)
            ? TelegramImportSourceType.Url
            : TelegramImportSourceType.Text;
    }

    private async Task<TelegramRouteResponse> BuildVocabularyTextResponseAsync(
        ConversationAgentResult result,
        ConversationScope scope,
        string locale,
        string rawInput,
        CancellationToken cancellationToken)
    {
        var formatted = _responseFormatter.Format(result);
        var pendingKey = BuildPendingSaveKey(scope);
        _pendingStateStore.VocabularySaves.TryRemove(pendingKey, out _);
        _pendingStateStore.VocabularyBatchSaves.TryRemove(pendingKey, out _);

        if (string.Equals(result.Intent, "command.unsupported", StringComparison.OrdinalIgnoreCase))
        {
            return new TelegramRouteResponse(
                result.Intent,
                BuildConsoleCommandMessage(rawInput, locale),
                InlineKeyboard(_navigationPresenter.BuildVocabularyKeyboard(locale)));
        }

        if (result.Items.Count == 0)
        {
            return new TelegramRouteResponse(result.Intent, formatted);
        }

        var saveMode = await _saveModePreferenceService.GetModeAsync(scope, cancellationToken);
        var saveCandidates = BuildSaveCandidates(result.Items);
        var previewWarnings = BuildPreviewWarnings(result.Items, locale);
        var wordValidationWarnings = BuildWordValidationWarnings(result.Items, locale);
        var foundInDeckInfo = BuildFoundInDeckInfo(result.Items, locale);

        if (!string.IsNullOrWhiteSpace(wordValidationWarnings))
        {
            formatted = result.Items.All(item => item.IsWordUnrecognized)
                ? wordValidationWarnings
                : AppendTextBlock(formatted, wordValidationWarnings);
        }

        if (!string.IsNullOrWhiteSpace(foundInDeckInfo))
        {
            formatted = AppendFoundInDeckInfo(formatted, foundInDeckInfo);
        }

        if (saveMode == VocabularySaveMode.Off)
        {
            if (saveCandidates.Count > 0)
            {
                formatted = AppendTextBlock(formatted, _navigationPresenter.GetText("vocab.save_mode_off_hint", locale));
            }

            if (!string.IsNullOrWhiteSpace(previewWarnings))
            {
                formatted = AppendTextBlock(formatted, previewWarnings);
            }

            return new TelegramRouteResponse(result.Intent, formatted);
        }

        if (saveMode == VocabularySaveMode.Ask)
        {
            if (saveCandidates.Count == 1 && !result.IsBatch)
            {
                var pending = saveCandidates[0];
                _pendingStateStore.VocabularySaves[pendingKey] = pending;

                var askText = _navigationPresenter.GetText(
                    "vocab.save.ask",
                    locale,
                    pending.RequestedWord,
                    pending.TargetDeckFileName);
                askText = EnsureQuestionMarker(askText);

                var message = formatted;
                if (!string.IsNullOrWhiteSpace(previewWarnings))
                {
                    message = AppendTextBlock(message, previewWarnings);
                }

                message = AppendTextBlock(message, askText);

                return new TelegramRouteResponse(
                    result.Intent,
                    message,
                    InlineKeyboard(_navigationPresenter.BuildVocabularySaveConfirmationKeyboard(locale)));
            }

            if (result.IsBatch && saveCandidates.Count > 0)
            {
                _pendingStateStore.VocabularyBatchSaves[pendingKey] = new PendingVocabularyBatchSaveRequest(saveCandidates);

                var askHint = _navigationPresenter.GetText("vocab.save_batch_ask_hint", locale);
                var askQuestion = EnsureQuestionMarker(
                    _navigationPresenter.GetText(
                        "vocab.save_batch_ask_question",
                        locale,
                        saveCandidates.Count));

                formatted = AppendFoundInDeckInfo(
                    formatted,
                    string.Concat(
                        SectionSeparator,
                        Environment.NewLine,
                        askHint,
                        Environment.NewLine,
                        askQuestion));
            }

            if (!string.IsNullOrWhiteSpace(previewWarnings))
            {
                formatted = AppendTextBlock(formatted, previewWarnings);
            }

            return result.IsBatch && saveCandidates.Count > 0
                ? new TelegramRouteResponse(
                    result.Intent,
                    formatted,
                    InlineKeyboard(_navigationPresenter.BuildVocabularyBatchSaveConfirmationKeyboard(locale)))
                : new TelegramRouteResponse(result.Intent, formatted);
        }

        var saveMessages = new List<string>();
        foreach (var pending in saveCandidates)
        {
            var appendResult = await _vocabularyPersistenceService.AppendFromAssistantReplyAsync(
                pending.RequestedWord,
                pending.AssistantReply,
                pending.TargetDeckFileName,
                pending.OverridePartOfSpeech,
                cancellationToken);

            saveMessages.Add(BuildAppendStatusMessage(appendResult, locale));
        }

        if (saveMessages.Count > 0)
        {
            formatted = AppendTextBlock(formatted, string.Join(Environment.NewLine, saveMessages));
        }

        if (!string.IsNullOrWhiteSpace(previewWarnings))
        {
            formatted = AppendTextBlock(formatted, previewWarnings);
        }

        return new TelegramRouteResponse(result.Intent, formatted);
    }

    private async Task<TelegramRouteResponse> HandleVocabularySaveConfirmationAsync(
        ConversationScope scope,
        string locale,
        bool saveRequested,
        CancellationToken cancellationToken)
    {
        var pendingKey = BuildPendingSaveKey(scope);
        if (!_pendingStateStore.VocabularySaves.TryGetValue(pendingKey, out var pending))
        {
            return new TelegramRouteResponse(
                "vocab.save.none",
                _navigationPresenter.GetText("vocab.no_pending_save", locale),
                InlineKeyboard(_navigationPresenter.BuildVocabularyKeyboard(locale)));
        }

        if (!saveRequested)
        {
            _pendingStateStore.VocabularySaves.TryRemove(pendingKey, out _);
            return new TelegramRouteResponse(
                "vocab.save.skip",
                _navigationPresenter.GetText("vocab.save.skip", locale),
                InlineKeyboard(_navigationPresenter.BuildVocabularyKeyboard(locale)));
        }

        var appendResult = await _vocabularyPersistenceService.AppendFromAssistantReplyAsync(
            pending.RequestedWord,
            pending.AssistantReply,
            pending.TargetDeckFileName,
            pending.OverridePartOfSpeech,
            cancellationToken);

        var queuedForGraphAuthorization = appendResult.Status == VocabularyAppendStatus.Error
            && IsGraphAuthRequiredMessage(appendResult.Message);
        var missingDeckError = appendResult.Status == VocabularyAppendStatus.Error
            && IsMissingDeckMessage(appendResult.Message);
        var keepPending = appendResult.Status == VocabularyAppendStatus.Error
            && !queuedForGraphAuthorization
            && !missingDeckError;
        if (!keepPending)
        {
            _pendingStateStore.VocabularySaves.TryRemove(pendingKey, out _);
        }

        var statusText = BuildAppendStatusMessage(appendResult, locale);
        if (keepPending)
        {
            var askText = _navigationPresenter.GetText(
                "vocab.save.ask",
                locale,
                pending.RequestedWord,
                pending.TargetDeckFileName);
            askText = EnsureQuestionMarker(askText);

            return new TelegramRouteResponse(
                "vocab.save.retry",
                AppendTextBlock(statusText, askText),
                InlineKeyboard(_navigationPresenter.BuildVocabularySaveConfirmationKeyboard(locale)));
        }

        return new TelegramRouteResponse(
            "vocab.save.done",
            statusText,
            InlineKeyboard(_navigationPresenter.BuildVocabularyKeyboard(locale)));
    }

    private List<PendingVocabularySaveRequest> BuildSaveCandidates(IReadOnlyList<ConversationAgentItemResult> items)
    {
        var candidates = new List<PendingVocabularySaveRequest>();

        foreach (var item in items)
        {
            if (item.FoundInDeck
                || item.AssistantCompletion is null)
            {
                continue;
            }

            var targetDeckFileName = ResolveSaveTargetDeckFileName(item);
            if (string.IsNullOrWhiteSpace(targetDeckFileName))
            {
                continue;
            }

            var overridePartOfSpeech = ResolvePrimaryPartOfSpeech(item.AssistantCompletion.Content);

            candidates.Add(new PendingVocabularySaveRequest(
                item.Input,
                item.AssistantCompletion.Content,
                targetDeckFileName,
                overridePartOfSpeech));
        }

        return candidates;
    }

    private string BuildPreviewWarnings(IReadOnlyList<ConversationAgentItemResult> items, string locale)
    {
        var warnings = new List<string>();

        foreach (var item in items)
        {
            if (item.FoundInDeck
                || item.AppendPreview is null
                || item.AppendPreview.Status == VocabularyAppendPreviewStatus.ReadyToAppend
                || string.IsNullOrWhiteSpace(item.AppendPreview.Message))
            {
                continue;
            }

            var localizedMessage = LocalizeGraphRelatedMessage(item.AppendPreview.Message, locale);
            warnings.Add($"⚠️ {localizedMessage}");
        }

        return string.Join(Environment.NewLine, warnings.Distinct(StringComparer.Ordinal));
    }

    private string BuildWordValidationWarnings(IReadOnlyList<ConversationAgentItemResult> items, string locale)
    {
        var warnings = new List<string>();

        foreach (var item in items)
        {
            if (!item.IsWordUnrecognized)
            {
                continue;
            }

            var suggestions = item.WordSuggestions
                .Where(suggestion => !string.IsNullOrWhiteSpace(suggestion))
                .Select(suggestion => suggestion.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(5)
                .ToList();

            if (suggestions.Count > 0)
            {
                var localizedMessage = _navigationPresenter.GetText(
                    "vocab.word_unrecognized_with_suggestions",
                    locale,
                    item.Input,
                    string.Join(", ", suggestions));
                warnings.Add(FormatWordSuggestionWarning(localizedMessage));
                continue;
            }

            warnings.Add(_navigationPresenter.GetText(
                "vocab.word_unrecognized",
                locale,
                item.Input));
        }

        return string.Join(Environment.NewLine, warnings.Distinct(StringComparer.Ordinal));
    }

    private string FormatWordSuggestionWarning(string localizedMessage)
    {
        var sentences = SentenceSplitRegex.Split(localizedMessage)
            .Select(static sentence => sentence.Trim())
            .Where(static sentence => !string.IsNullOrWhiteSpace(sentence))
            .ToList();

        if (sentences.Count < 2)
        {
            return localizedMessage;
        }

        var lines = new List<string>(sentences.Count)
        {
            sentences[0],
            EnsureQuestionMarker(sentences[1])
        };

        if (sentences.Count > 2)
        {
            lines.AddRange(sentences.Skip(2));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private string BuildFoundInDeckInfo(IReadOnlyList<ConversationAgentItemResult> items, string locale)
    {
        var foundItems = items
            .Where(item => item.FoundInDeck && item.Lookup.Matches.Count > 0)
            .ToList();

        if (foundItems.Count == 0)
        {
            return string.Empty;
        }

        if (foundItems.Count == 1)
        {
            var match = foundItems[0].Lookup.Matches[0];
            return _navigationPresenter.GetText(
                "vocab.found_in_deck_single",
                locale,
                match.DeckFileName,
                match.RowNumber);
        }

        var lines = new List<string>
        {
            SectionSeparator,
            _navigationPresenter.GetText("vocab.found_in_deck_multi_title", locale)
        };

        foreach (var item in foundItems)
        {
            var match = item.Lookup.Matches[0];
            lines.Add(_navigationPresenter.GetText(
                "vocab.found_in_deck_multi_item",
                locale,
                item.Input,
                match.DeckFileName,
                match.RowNumber));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private string BuildAppendStatusMessage(VocabularyAppendResult appendResult, string locale)
    {
        if (appendResult.Status == VocabularyAppendStatus.Error
            && IsGraphAuthRequiredMessage(appendResult.Message))
        {
            return _navigationPresenter.GetText("vocab.save_queued_waiting_auth", locale);
        }

        if (appendResult.Status == VocabularyAppendStatus.Error
            && IsMissingDeckMessage(appendResult.Message))
        {
            var deckName = TryExtractDeckFileName(appendResult.Message) ?? _navigationPresenter.GetText("vocab.deck_unknown", locale);
            return _navigationPresenter.GetText("vocab.save_missing_deck", locale, WebUtility.HtmlEncode(deckName));
        }

        return appendResult.Status switch
        {
            VocabularyAppendStatus.Added when appendResult.Entry is not null => _navigationPresenter.GetText(
                "vocab.save.saved",
                locale,
                appendResult.Entry.DeckFileName,
                appendResult.Entry.RowNumber),
            VocabularyAppendStatus.DuplicateFound => _navigationPresenter.GetText("vocab.save.duplicate", locale),
            _ => _navigationPresenter.GetText(
                "vocab.save_failed",
                locale,
                LocalizeSaveFailureMessage(appendResult.Message, locale))
        };
    }

    private string BuildConsoleCommandMessage(string input, string locale)
    {
        var command = string.IsNullOrWhiteSpace(input)
            ? "/command"
            : input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];

        if (command.StartsWith("/graph", StringComparison.OrdinalIgnoreCase))
        {
            return _navigationPresenter.GetText("command.console_only_graph", locale, command);
        }

        return _navigationPresenter.GetText("command.console_only_generic", locale, command);
    }

    private string LocalizeSaveFailureMessage(string? message, string locale)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "Unknown error";
        }

        if (IsGraphNotConfiguredMessage(message))
        {
            return _navigationPresenter.GetText("onedrive.error_not_configured", locale);
        }

        if (IsGraphAuthRequiredMessage(message) || IsGraphTokenExpiredMessage(message))
        {
            return _navigationPresenter.GetText("vocab.graph_save_setup_required", locale);
        }

        if (IsMissingDeckMessage(message))
        {
            var deckName = TryExtractDeckFileName(message) ?? _navigationPresenter.GetText("vocab.deck_unknown", locale);
            return _navigationPresenter.GetText("vocab.save_missing_deck", locale, WebUtility.HtmlEncode(deckName));
        }

        return LocalizeGraphRelatedMessage(message, locale);
    }

    private string LocalizeGraphRelatedMessage(string? message, string locale)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return string.Empty;
        }

        if (IsGraphNotConfiguredMessage(message))
        {
            return _navigationPresenter.GetText("onedrive.error_not_configured", locale);
        }

        if (IsGraphTokenExpiredMessage(message))
        {
            return _navigationPresenter.GetText("onedrive.error_expired", locale);
        }

        if (message.Contains("timed out", StringComparison.OrdinalIgnoreCase))
        {
            return _navigationPresenter.GetText("onedrive.error_timed_out", locale);
        }

        if (message.Contains("declined", StringComparison.OrdinalIgnoreCase))
        {
            return _navigationPresenter.GetText("onedrive.error_declined", locale);
        }

        if (IsGraphAuthRequiredMessage(message))
        {
            return _navigationPresenter.GetText("onedrive.error_not_authenticated", locale);
        }

        return message.Trim();
    }

    private static bool IsGraphNotConfiguredMessage(string? message)
        => !string.IsNullOrWhiteSpace(message)
           && message.Contains("not configured", StringComparison.OrdinalIgnoreCase);

    private static bool IsGraphTokenExpiredMessage(string? message)
        => !string.IsNullOrWhiteSpace(message)
           && (message.Contains("token expired", StringComparison.OrdinalIgnoreCase)
               || message.Contains("expired token", StringComparison.OrdinalIgnoreCase));

    private static bool IsGraphAuthRequiredMessage(string? message)
        => !string.IsNullOrWhiteSpace(message)
           && (message.Contains("graph authentication is required", StringComparison.OrdinalIgnoreCase)
               || message.Contains("not authenticated", StringComparison.OrdinalIgnoreCase)
               || message.Contains("authorization failed", StringComparison.OrdinalIgnoreCase)
               || message.Contains("run /graph login", StringComparison.OrdinalIgnoreCase)
               || message.Contains("use /graph login", StringComparison.OrdinalIgnoreCase));

    private static bool IsMissingDeckMessage(string? message)
        => !string.IsNullOrWhiteSpace(message)
           && (message.Contains("could not resolve onedrive target deck", StringComparison.OrdinalIgnoreCase)
               || message.Contains("not writable or was not found", StringComparison.OrdinalIgnoreCase)
               || message.Contains("required deck files are missing", StringComparison.OrdinalIgnoreCase));

    private static string? TryExtractDeckFileName(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var firstQuote = message.IndexOf('\'');
        if (firstQuote < 0)
        {
            return null;
        }

        var secondQuote = message.IndexOf('\'', firstQuote + 1);
        if (secondQuote <= firstQuote + 1)
        {
            return null;
        }

        return message[(firstQuote + 1)..secondQuote].Trim();
    }

    private string ResolveSaveTargetDeckFileName(ConversationAgentItemResult item)
    {
        if (item.AppendPreview?.Status == VocabularyAppendPreviewStatus.ReadyToAppend
            && !string.IsNullOrWhiteSpace(item.AppendPreview.TargetDeckFileName))
        {
            return item.AppendPreview.TargetDeckFileName;
        }

        var marker = item.AssistantCompletion is null
            ? null
            : ResolvePrimaryPartOfSpeech(item.AssistantCompletion.Content);

        var fromMarker = ResolveDeckFileNameByMarker(marker);
        if (!string.IsNullOrWhiteSpace(fromMarker))
        {
            return fromMarker;
        }

        return _vocabularyDeckOptions.FallbackDeckFileName;
    }

    private string? ResolvePrimaryPartOfSpeech(string assistantReply)
    {
        if (string.IsNullOrWhiteSpace(assistantReply))
        {
            return null;
        }

        if (_vocabularyReplyParser.TryParse(assistantReply, out var parsed)
            && parsed is not null
            && parsed.PartsOfSpeech.Count > 0)
        {
            return NormalizeMarker(parsed.PartsOfSpeech[0]);
        }

        return null;
    }

    private string? ResolveDeckFileNameByMarker(string? marker)
    {
        return NormalizeMarker(marker) switch
        {
            "n" => _vocabularyDeckOptions.NounDeckFileName,
            "v" => _vocabularyDeckOptions.VerbDeckFileName,
            "iv" => _vocabularyDeckOptions.IrregularVerbDeckFileName,
            "pv" => _vocabularyDeckOptions.PhrasalVerbDeckFileName,
            "adj" => _vocabularyDeckOptions.AdjectiveDeckFileName,
            "adv" => _vocabularyDeckOptions.AdverbDeckFileName,
            "prep" => _vocabularyDeckOptions.PrepositionDeckFileName,
            "conj" => _vocabularyDeckOptions.ConjunctionDeckFileName,
            "pron" => _vocabularyDeckOptions.PronounDeckFileName,
            "pe" => _vocabularyDeckOptions.PersistentExpressionDeckFileName,
            _ => null
        };
    }

    private static string? NormalizeMarker(string? marker)
    {
        if (string.IsNullOrWhiteSpace(marker))
        {
            return null;
        }

        return marker.Trim().ToLowerInvariant();
    }

    private static string AppendTextBlock(string source, string extra)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return extra?.Trim() ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(extra))
        {
            return source;
        }

        return string.Concat(source.TrimEnd(), Environment.NewLine, Environment.NewLine, extra.Trim());
    }

    private static string AppendFoundInDeckInfo(string source, string foundInDeckInfo)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return foundInDeckInfo?.Trim() ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(foundInDeckInfo))
        {
            return source;
        }

        var extra = foundInDeckInfo.Trim();
        if (extra.StartsWith(SectionSeparator, StringComparison.Ordinal))
        {
            return string.Concat(source.TrimEnd(), Environment.NewLine, extra);
        }

        return AppendTextBlock(source, extra);
    }

    private async Task<TelegramRouteResponse> BuildVocabularySectionResponseAsync(string locale, CancellationToken cancellationToken)
    {
        var count = await _vocabularyCardRepository.CountAllAsync(cancellationToken);
        var title = _navigationPresenter.GetText("menu.vocabulary.title", locale, count);

        return new TelegramRouteResponse(
            "vocab.section",
            title,
            InlineKeyboard(_navigationPresenter.BuildVocabularyKeyboard(locale)));
    }

    private async Task<TelegramRouteResponse> BuildVocabularyStatisticsResponseAsync(string locale, CancellationToken cancellationToken)
    {
        var total = await _vocabularyCardRepository.CountAllAsync(cancellationToken);
        if (total == 0)
        {
            return new TelegramRouteResponse(
                "vocab.stats",
                _navigationPresenter.GetText("vocab.stats.empty", locale),
                InlineKeyboard(_navigationPresenter.BuildVocabularyKeyboard(locale)));
        }

        var markerStats = await _vocabularyCardRepository.GetPartOfSpeechStatsAsync(cancellationToken);
        var deckStats = await _vocabularyCardRepository.GetDeckStatsAsync(cancellationToken);

        var normalizedMarkerStats = markerStats
            .Where(item => item.Count > 0)
            .Select(item => new
            {
                Marker = VocabularyPartOfSpeechCatalog.NormalizeOrNull(item.Marker)
                    ?? item.Marker?.Trim().ToLowerInvariant()
                    ?? string.Empty,
                item.Count
            })
            .GroupBy(item => item.Marker, StringComparer.Ordinal)
            .Select(group => new VocabularyPartOfSpeechStat(group.Key, group.Sum(item => item.Count)))
            .ToList();
        var nonEmptyDeckStats = deckStats
            .Where(item => item.Count > 0)
            .ToList();

        var markerCounts = normalizedMarkerStats.ToDictionary(item => item.Marker ?? string.Empty, item => item.Count, StringComparer.Ordinal);
        var primaryMarkerSet = new HashSet<string>(PrimaryPartOfSpeechMarkers, StringComparer.Ordinal);
        var primaryMarkerLines = PrimaryPartOfSpeechMarkers
            .Where(marker => markerCounts.TryGetValue(marker, out var count) && count > 0)
            .Select(marker => $"{GetPartOfSpeechLabel(locale, marker)}: {markerCounts[marker]}")
            .ToList();
        var otherPartOfSpeechTotal = normalizedMarkerStats
            .Where(item => !primaryMarkerSet.Contains(item.Marker ?? string.Empty))
            .Sum(item => item.Count);

        var topDecks = nonEmptyDeckStats.Take(10).ToList();
        var remainingDeckCount = Math.Max(0, nonEmptyDeckStats.Count - topDecks.Count);

        var lines = new List<string>
        {
            _navigationPresenter.GetText("vocab.stats.title", locale),
            string.Empty,
            _navigationPresenter.GetText("vocab.stats.total", locale, total),
            _navigationPresenter.GetText("vocab.stats.summary", locale, nonEmptyDeckStats.Count, normalizedMarkerStats.Count),
            string.Empty,
            _navigationPresenter.GetText("vocab.stats.by_marker", locale)
        };

        if (primaryMarkerLines.Count == 0 && otherPartOfSpeechTotal == 0)
        {
            lines.Add(_navigationPresenter.GetText("vocab.stats.no_data", locale));
        }
        else
        {
            lines.AddRange(primaryMarkerLines);

            if (otherPartOfSpeechTotal > 0)
            {
                lines.Add(GetAndMorePartOfSpeechLine(locale, otherPartOfSpeechTotal));
            }
        }

        lines.Add(string.Empty);
        lines.Add(_navigationPresenter.GetText("vocab.stats.top_decks", locale));

        if (topDecks.Count == 0)
        {
            lines.Add(_navigationPresenter.GetText("vocab.stats.no_data", locale));
        }
        else
        {
            for (var i = 0; i < topDecks.Count; i++)
            {
                lines.Add(_navigationPresenter.GetText(
                    "vocab.stats.deck_item",
                    locale,
                    i + 1,
                    WebUtility.HtmlEncode(topDecks[i].DeckFileName),
                    topDecks[i].Count));
            }
        }

        if (remainingDeckCount > 0)
        {
            lines.Add(_navigationPresenter.GetText("vocab.stats.and_more_decks", locale, remainingDeckCount));
        }

        return new TelegramRouteResponse(
            "vocab.stats",
            string.Join(Environment.NewLine, lines),
            InlineKeyboard(_navigationPresenter.BuildVocabularyKeyboard(locale)));
    }

    private async Task<TelegramRouteResponse> HandleVocabularyBatchSaveConfirmationAsync(
        ConversationScope scope,
        string locale,
        bool saveRequested,
        CancellationToken cancellationToken)
    {
        var pendingKey = BuildPendingSaveKey(scope);
        if (!_pendingStateStore.VocabularyBatchSaves.TryGetValue(pendingKey, out var pendingBatch)
            || pendingBatch.Items.Count == 0)
        {
            return new TelegramRouteResponse(
                "vocab.save.none",
                _navigationPresenter.GetText("vocab.no_pending_save", locale),
                InlineKeyboard(_navigationPresenter.BuildVocabularyKeyboard(locale)));
        }

        if (!saveRequested)
        {
            _pendingStateStore.VocabularyBatchSaves.TryRemove(pendingKey, out _);
            return new TelegramRouteResponse(
                "vocab.save.batch.skip",
                _navigationPresenter.GetText("vocab.save_batch_skip", locale),
                InlineKeyboard(_navigationPresenter.BuildVocabularyKeyboard(locale)));
        }

        _pendingStateStore.VocabularyBatchSaves.TryRemove(pendingKey, out _);

        var saveMessages = new List<string>();
        var saved = 0;
        var duplicate = 0;
        var failed = 0;

        foreach (var pending in pendingBatch.Items)
        {
            var appendResult = await _vocabularyPersistenceService.AppendFromAssistantReplyAsync(
                pending.RequestedWord,
                pending.AssistantReply,
                pending.TargetDeckFileName,
                pending.OverridePartOfSpeech,
                cancellationToken);

            switch (appendResult.Status)
            {
                case VocabularyAppendStatus.Added:
                    saved++;
                    break;
                case VocabularyAppendStatus.DuplicateFound:
                    duplicate++;
                    break;
                case VocabularyAppendStatus.Error:
                    failed++;
                    break;
            }

            saveMessages.Add(BuildAppendStatusMessage(appendResult, locale));
        }

        var summary = _navigationPresenter.GetText(
            "vocab.save_batch_done",
            locale,
            saved,
            duplicate,
            failed);

        var details = string.Join(Environment.NewLine, saveMessages);
        return new TelegramRouteResponse(
            "vocab.save.batch.done",
            string.Concat(summary, Environment.NewLine, Environment.NewLine, details),
            InlineKeyboard(_navigationPresenter.BuildVocabularyKeyboard(locale)));
    }

    private static string GetPartOfSpeechLabel(string locale, string marker)
    {
        var normalizedLocale = LocalizationConstants.NormalizeLocaleCode(locale);

        return normalizedLocale switch
        {
            LocalizationConstants.UkrainianLocale => marker switch
            {
                "n" => "Іменники",
                "v" => "Дієслова",
                "pv" => "Фразові дієслова",
                "iv" => "Неправильні дієслова",
                "adv" => "Прислівники",
                "prep" => "Прийменники",
                _ => marker
            },
            LocalizationConstants.SpanishLocale => marker switch
            {
                "n" => "Sustantivos",
                "v" => "Verbos",
                "pv" => "Verbos frasales",
                "iv" => "Verbos irregulares",
                "adv" => "Adverbios",
                "prep" => "Preposiciones",
                _ => marker
            },
            LocalizationConstants.FrenchLocale => marker switch
            {
                "n" => "Noms",
                "v" => "Verbes",
                "pv" => "Verbes à particule",
                "iv" => "Verbes irréguliers",
                "adv" => "Adverbes",
                "prep" => "Prépositions",
                _ => marker
            },
            LocalizationConstants.GermanLocale => marker switch
            {
                "n" => "Substantive",
                "v" => "Verben",
                "pv" => "Phrasalverben",
                "iv" => "Unregelmäßige Verben",
                "adv" => "Adverbien",
                "prep" => "Präpositionen",
                _ => marker
            },
            LocalizationConstants.PolishLocale => marker switch
            {
                "n" => "Rzeczowniki",
                "v" => "Czasowniki",
                "pv" => "Czasowniki frazowe",
                "iv" => "Czasowniki nieregularne",
                "adv" => "Przysłówki",
                "prep" => "Przyimki",
                _ => marker
            },
            _ => marker switch
            {
                "n" => "Nouns",
                "v" => "Verbs",
                "pv" => "Phrasal verbs",
                "iv" => "Irregular verbs",
                "adv" => "Adverbs",
                "prep" => "Prepositions",
                _ => marker
            }
        };
    }

    private static string GetAndMorePartOfSpeechLine(string locale, int count)
    {
        var normalizedLocale = LocalizationConstants.NormalizeLocaleCode(locale);
        var template = normalizedLocale switch
        {
            LocalizationConstants.UkrainianLocale => "... і ще {0}",
            LocalizationConstants.SpanishLocale => "... y {0} más",
            LocalizationConstants.FrenchLocale => "... et encore {0}",
            LocalizationConstants.GermanLocale => "... und noch {0}",
            LocalizationConstants.PolishLocale => "... i jeszcze {0}",
            _ => "... and {0} more"
        };

        return string.Format(template, count);
    }

    private async Task<TelegramRouteResponse> HandleVocabularyStatsCallbackAsync(
        string locale,
        CancellationToken cancellationToken)
    {
        try
        {
            return await BuildVocabularyStatisticsResponseAsync(locale, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to build vocabulary statistics response.");

            return new TelegramRouteResponse(
                "vocab.stats.failed",
                _navigationPresenter.GetText(
                    "onedrive.operation_failed",
                    locale,
                    WebUtility.HtmlEncode(LocalizeGraphRelatedMessage(ex.Message, locale))),
                InlineKeyboard(_navigationPresenter.BuildVocabularyKeyboard(locale)),
                IsHtml: true);
        }
    }

    private TelegramRouteResponse BuildBatchModeResponse(
        ConversationScope scope,
        string locale)
    {
        _pendingStateStore.VocabularyUrlSessions.TryRemove(BuildPendingUrlSessionKey(scope), out _);
        return new TelegramRouteResponse(
            "vocab.batch",
            _navigationPresenter.GetText("vocab.batch.prompt", locale),
            InlineKeyboard(_navigationPresenter.BuildVocabularyKeyboard(locale)));
    }

    private async Task<TelegramRouteResponse> BuildSettingsSectionResponseAsync(
        ConversationScope scope,
        string locale,
        CancellationToken cancellationToken)
    {
        var saveMode = await _saveModePreferenceService.GetModeAsync(scope, cancellationToken);
        var storageMode = VocabularyStorageMode.Graph;
        _storageModeProvider.SetMode(storageMode);
        var graphStatus = await _graphAuthService.GetStatusAsync(cancellationToken);
        var languageLabel = StripLeadingDecorations(_navigationPresenter.GetText("settings.language", locale));
        var saveModeLabel = StripLeadingDecorations(_navigationPresenter.GetText("settings.save_mode", locale));
        var storageModeLabel = StripLeadingDecorations(_navigationPresenter.GetText("settings.storage_mode", locale));
        var oneDriveLabel = StripLeadingDecorations(_navigationPresenter.GetText("settings.onedrive", locale));
        var notionLabel = StripLeadingDecorations(_navigationPresenter.GetText("settings.notion", locale));
        var notionStatusKey = ResolveNotionSettingsStatusKey();
        var oneDriveStatus = StripStatusPrefix(
            _navigationPresenter.GetText(
                graphStatus.IsAuthenticated ? "onedrive.status_connected" : "onedrive.status_disconnected",
                locale));
        var notionStatus = StripStatusPrefix(_navigationPresenter.GetText(notionStatusKey, locale));

        var text = string.Join(Environment.NewLine, new[]
        {
            _navigationPresenter.GetText("settings.title", locale),
            string.Empty,
            $"• <b>{WebUtility.HtmlEncode(languageLabel)}:</b> {WebUtility.HtmlEncode(_navigationPresenter.GetLanguageDisplayName(locale))}",
            $"• <b>{WebUtility.HtmlEncode(saveModeLabel)}:</b> <b>{WebUtility.HtmlEncode(_saveModePreferenceService.ToText(saveMode))}</b>",
            $"• <b>{WebUtility.HtmlEncode(storageModeLabel)}:</b> <b>{WebUtility.HtmlEncode(_storageModeProvider.ToText(storageMode))}</b>",
            $"• <b>{WebUtility.HtmlEncode(oneDriveLabel)}:</b> {WebUtility.HtmlEncode(oneDriveStatus)}",
            $"• <b>{WebUtility.HtmlEncode(notionLabel)}:</b> {WebUtility.HtmlEncode(notionStatus)}"
        });

        return new TelegramRouteResponse(
            "settings.section",
            text,
            InlineKeyboard(_navigationPresenter.BuildSettingsKeyboard(locale)),
            IsHtml: true);
    }

    private TelegramRouteResponse BuildNotionSectionResponse(string locale)
    {
        var notionTitle = _navigationPresenter.GetText("notion.title", locale);
        var vocabularyLabel = _navigationPresenter.GetText("notion.vocabulary", locale);
        var foodLabel = _navigationPresenter.GetText("notion.food", locale);

        var vocabularyEnabled = _notionOptions.Enabled;
        var vocabularyConfigured = IsNotionVocabularyConfigured();
        var vocabularyStatus = _navigationPresenter.GetText(
            vocabularyEnabled ? "notion.status_enabled" : "notion.status_disabled",
            locale);
        var vocabularyConfiguredStatus = _navigationPresenter.GetText(
            vocabularyConfigured ? "notion.configured_yes" : "notion.configured_no",
            locale);
        var vocabularyWorkerStatus = _navigationPresenter.GetText(
            _notionSyncWorkerOptions.Enabled ? "notion.worker_enabled" : "notion.worker_disabled",
            locale);

        var foodEnabled = _notionFoodOptions.Enabled;
        var foodConfigured = _notionFoodOptions.IsConfigured;
        var foodStatus = _navigationPresenter.GetText(
            foodEnabled ? "notion.status_enabled" : "notion.status_disabled",
            locale);
        var foodConfiguredStatus = _navigationPresenter.GetText(
            foodConfigured ? "notion.configured_yes" : "notion.configured_no",
            locale);
        var foodWorkerStatus = _navigationPresenter.GetText(
            _foodSyncWorkerOptions.Enabled ? "notion.worker_enabled" : "notion.worker_disabled",
            locale);

        var lines = new List<string>
        {
            notionTitle,
            string.Empty,
            $"• <b>{WebUtility.HtmlEncode(vocabularyLabel)}</b>: {WebUtility.HtmlEncode(vocabularyStatus)}",
            $"  {WebUtility.HtmlEncode(vocabularyConfiguredStatus)}",
            $"  {WebUtility.HtmlEncode(vocabularyWorkerStatus)}",
            string.Empty,
            $"• <b>{WebUtility.HtmlEncode(foodLabel)}</b>: {WebUtility.HtmlEncode(foodStatus)}",
            $"  {WebUtility.HtmlEncode(foodConfiguredStatus)}",
            $"  {WebUtility.HtmlEncode(foodWorkerStatus)}",
            string.Empty,
            WebUtility.HtmlEncode(_navigationPresenter.GetText("notion.tip", locale))
        };

        return new TelegramRouteResponse(
            "settings.notion",
            string.Join(Environment.NewLine, lines),
            InlineKeyboard(_navigationPresenter.BuildNotionKeyboard(locale)),
            IsHtml: true);
    }

    private string ResolveNotionSettingsStatusKey()
    {
        var hasEnabledNotionFlow = _notionOptions.Enabled || _notionFoodOptions.Enabled;
        if (!hasEnabledNotionFlow)
        {
            return "settings.notion_disabled";
        }

        var vocabularyMissingConfig = _notionOptions.Enabled && !IsNotionVocabularyConfigured();
        var foodMissingConfig = _notionFoodOptions.Enabled && !_notionFoodOptions.IsConfigured;
        if (vocabularyMissingConfig || foodMissingConfig)
        {
            return "settings.notion_partial";
        }

        return "settings.notion_enabled";
    }

    private bool IsNotionVocabularyConfigured()
    {
        return !string.IsNullOrWhiteSpace(_notionOptions.ApiKey)
            && !string.IsNullOrWhiteSpace(_notionOptions.DatabaseId);
    }

    private static string StripLeadingDecorations(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var withoutPrefix = LeadingDecorationRegex.Replace(value, string.Empty);
        return withoutPrefix.Trim();
    }

    private static string StripStatusPrefix(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        var colonIndex = trimmed.IndexOf(':');
        return colonIndex >= 0 && colonIndex < trimmed.Length - 1
            ? trimmed[(colonIndex + 1)..].Trim()
            : trimmed;
    }

    private static string EnsureQuestionMarker(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmedStart = value.TrimStart();
        if (trimmedStart.StartsWith(QuestionMarker, StringComparison.Ordinal))
        {
            return value;
        }

        if (WarningMarkers.Any(marker => trimmedStart.StartsWith(marker, StringComparison.Ordinal)))
        {
            return $"{QuestionMarker}{Environment.NewLine}{value}";
        }

        return $"{QuestionMarker} {value}";
    }

    private async Task TrySendProgressMessageAsync(
        long chatId,
        int? messageThreadId,
        string text,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var sendResult = await _telegramBotSender.SendTextAsync(
            chatId,
            WebUtility.HtmlEncode(text),
            EnsureHtmlParseMode(options: null),
            messageThreadId,
            cancellationToken);

        if (!sendResult.Succeeded)
        {
            _logger.LogWarning(
                "Telegram progress message send failed. ChatId={ChatId}; Error={Error}",
                chatId,
                sendResult.ErrorMessage);
        }
    }

    private async Task<TelegramRouteResponse> BuildSaveModeResponseAsync(
        ConversationScope scope,
        string locale,
        CancellationToken cancellationToken)
    {
        var currentMode = await _saveModePreferenceService.GetModeAsync(scope, cancellationToken);
        var text = _navigationPresenter.GetText("savemode.title", locale, _saveModePreferenceService.ToText(currentMode));

        return new TelegramRouteResponse(
            "settings.savemode",
            text,
            InlineKeyboard(_navigationPresenter.BuildSaveModeKeyboard(locale)),
            IsHtml: true);
    }

    private async Task<TelegramRouteResponse> BuildOneDriveResponseAsync(
        ConversationScope scope,
        string locale,
        bool includeCheckStatusButton,
        CancellationToken cancellationToken)
    {
        await _navigationStateService.SetCurrentSectionAsync(scope.Channel, scope.UserId, scope.ConversationId, NavigationSections.Settings, cancellationToken);

        var status = await _graphAuthService.GetStatusAsync(cancellationToken);
        var lines = new List<string>
        {
            _navigationPresenter.GetText("onedrive.title", locale),
            string.Empty,
            _navigationPresenter.GetText(status.IsAuthenticated ? "onedrive.status_connected" : "onedrive.status_disconnected", locale)
        };

        if (!status.IsConfigured || (!status.IsAuthenticated && !string.IsNullOrWhiteSpace(status.Message)))
        {
            lines.Add(string.Empty);
            lines.Add(WebUtility.HtmlEncode(LocalizeGraphRelatedMessage(status.Message, locale)));
        }

        if (status.IsAuthenticated)
        {
            try
            {
                var missingDeckNotice = await BuildMissingConfiguredDecksNoticeAsync(locale, cancellationToken);
                if (!string.IsNullOrWhiteSpace(missingDeckNotice))
                {
                    lines.Add(string.Empty);
                    lines.Add(missingDeckNotice);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not evaluate OneDrive deck health notice.");
            }
        }

        return new TelegramRouteResponse(
            "settings.onedrive",
            string.Join(Environment.NewLine, lines),
            InlineKeyboard(_navigationPresenter.BuildOneDriveKeyboard(locale, status.IsAuthenticated, includeCheckStatusButton)),
            IsHtml: true);
    }

    private async Task<string?> BuildMissingConfiguredDecksNoticeAsync(
        string locale,
        CancellationToken cancellationToken)
    {
        _storageModeProvider.SetMode(VocabularyStorageMode.Graph);
        var writableDecks = await _vocabularyDeckService.GetWritableDeckFilesAsync(cancellationToken);
        var writableDeckNames = writableDecks
            .Select(deck => deck.FileName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var configuredTargets = GetConfiguredDeckTargets();
        var missingTargets = configuredTargets
            .Where(target => !writableDeckNames.Contains(target.DeckFileName))
            .GroupBy(target => target.DeckFileName, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ConfiguredDeckTarget(
                string.Join(", ", group.Select(item => item.Marker).Distinct(StringComparer.OrdinalIgnoreCase)),
                group.First().DeckFileName))
            .ToList();

        if (missingTargets.Count == 0)
        {
            return null;
        }

        var lines = new List<string>
        {
            _navigationPresenter.GetText("onedrive.decks_missing_title", locale, missingTargets.Count)
        };

        foreach (var target in missingTargets)
        {
            lines.Add(_navigationPresenter.GetText(
                "onedrive.decks_missing_item",
                locale,
                WebUtility.HtmlEncode(target.Marker),
                WebUtility.HtmlEncode(target.DeckFileName)));
        }

        lines.Add(_navigationPresenter.GetText("onedrive.decks_missing_hint", locale));
        return string.Join(Environment.NewLine, lines);
    }

    private List<ConfiguredDeckTarget> GetConfiguredDeckTargets()
    {
        return new List<ConfiguredDeckTarget>
        {
            new("n", _vocabularyDeckOptions.NounDeckFileName),
            new("v", _vocabularyDeckOptions.VerbDeckFileName),
            new("iv", _vocabularyDeckOptions.IrregularVerbDeckFileName),
            new("pv", _vocabularyDeckOptions.PhrasalVerbDeckFileName),
            new("adj", _vocabularyDeckOptions.AdjectiveDeckFileName),
            new("adv", _vocabularyDeckOptions.AdverbDeckFileName),
            new("prep", _vocabularyDeckOptions.PrepositionDeckFileName),
            new("conj", _vocabularyDeckOptions.ConjunctionDeckFileName),
            new("pron", _vocabularyDeckOptions.PronounDeckFileName),
            new("pe", _vocabularyDeckOptions.PersistentExpressionDeckFileName),
            new("fallback", _vocabularyDeckOptions.FallbackDeckFileName)
        }
        .Where(target => !string.IsNullOrWhiteSpace(target.DeckFileName))
        .Select(target => target with { DeckFileName = target.DeckFileName.Trim() })
        .ToList();
    }

    private async Task<string> PrependReleaseAnnouncementIfNeededAsync(
        ConversationScope scope,
        string locale,
        string outboundText,
        CancellationToken cancellationToken)
    {
        if (!_releaseAnnouncementOptions.Enabled)
        {
            return outboundText;
        }

        var version = ResolveReleaseAnnouncementVersion();
        if (string.IsNullOrWhiteSpace(version))
        {
            _logger.LogDebug("Release announcement skipped: no resolved version.");
            return outboundText;
        }

        if (_userMemoryRepository is null || _unitOfWork is null)
        {
            return outboundText;
        }

        try
        {
            var entry = await _userMemoryRepository.GetByKeyAsync(
                UserPreferenceMemoryKeys.ReleaseLastNotifiedVersion,
                scope.Channel,
                scope.UserId,
                cancellationToken);

            var normalizedLocale = LocalizationConstants.NormalizeLocaleCode(locale);
            var resolvedNotes = ResolveReleaseAnnouncementNotes(normalizedLocale);
            var releaseToken = BuildReleaseNotificationToken(version, normalizedLocale, resolvedNotes);

            if (entry is not null)
            {
                var storedToken = entry.Value?.Trim() ?? string.Empty;
                if (string.Equals(storedToken, releaseToken, StringComparison.Ordinal))
                {
                    return outboundText;
                }

                // Backward compatibility for entries that stored only the version.
                if (string.Equals(storedToken, version, StringComparison.Ordinal)
                    && string.Equals(normalizedLocale, LocalizationConstants.EnglishLocale, StringComparison.Ordinal))
                {
                    return outboundText;
                }
            }

            if (entry is null)
            {
                await _userMemoryRepository.AddAsync(new UserMemoryEntry
                {
                    Key = UserPreferenceMemoryKeys.ReleaseLastNotifiedVersion,
                    Value = releaseToken,
                    Confidence = 1.0,
                    IsActive = false,
                    LastSeenAtUtc = DateTimeOffset.UtcNow,
                    Channel = scope.Channel,
                    UserId = scope.UserId
                }, cancellationToken);
            }
            else
            {
                entry.Value = releaseToken;
                entry.Confidence = 1.0;
                entry.IsActive = false;
                entry.LastSeenAtUtc = DateTimeOffset.UtcNow;
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            var releaseBlock = BuildReleaseAnnouncementBlock(locale, version, resolvedNotes);
            _logger.LogInformation(
                "Release announcement queued for user. Channel={Channel}; UserId={UserId}; Version={Version}",
                scope.Channel,
                scope.UserId,
                version);
            return string.Concat(releaseBlock, Environment.NewLine, Environment.NewLine, outboundText);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to append release announcement. Channel={Channel}; UserId={UserId}", scope.Channel, scope.UserId);
            return outboundText;
        }
    }

    private string? ResolveReleaseAnnouncementVersion()
    {
        var configured = _releaseAnnouncementOptions.Version?.Trim();
        if (!string.IsNullOrWhiteSpace(configured)
            && !string.Equals(configured, AutomaticReleaseVersionToken, StringComparison.OrdinalIgnoreCase))
        {
            return NormalizeReleaseVersion(configured);
        }

        foreach (var key in ReleaseVersionEnvironmentKeys)
        {
            var value = Environment.GetEnvironmentVariable(key)?.Trim();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return NormalizeReleaseVersion(value);
            }
        }

        var informational = typeof(TelegramController)
            .Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?.Trim();

        if (!string.IsNullOrWhiteSpace(informational))
        {
            return NormalizeReleaseVersion(informational);
        }

        return NormalizeReleaseVersion(typeof(TelegramController).Assembly.GetName().Version?.ToString());
    }

    private DateTimeOffset ResolveReleaseAnnouncementDate()
    {
        var configured = _releaseAnnouncementOptions.ReleaseDate?.Trim();
        if (!string.IsNullOrWhiteSpace(configured)
            && DateTimeOffset.TryParse(configured, out var configuredDate))
        {
            return configuredDate;
        }

        foreach (var key in ReleaseDateEnvironmentKeys)
        {
            var value = Environment.GetEnvironmentVariable(key)?.Trim();
            if (!string.IsNullOrWhiteSpace(value)
                && DateTimeOffset.TryParse(value, out var environmentDate))
            {
                return environmentDate;
            }
        }

        return DateTimeOffset.UtcNow;
    }

    private static string? NormalizeReleaseVersion(string? rawVersion)
    {
        if (string.IsNullOrWhiteSpace(rawVersion))
        {
            return null;
        }

        var value = rawVersion.Trim();
        var plusIndex = value.IndexOf('+', StringComparison.Ordinal);
        if (plusIndex > 0)
        {
            value = value[..plusIndex];
        }

        if (Guid.TryParse(value, out _))
        {
            return $"build-{value[..8]}";
        }

        if (value.Length >= 12
            && value.All(static c => char.IsAsciiHexDigit(c)))
        {
            return value[..8].ToLowerInvariant();
        }

        return value;
    }

    private static string BuildReleaseNotificationToken(string version, string locale, string notes)
    {
        var normalizedLocale = LocalizationConstants.NormalizeLocaleCode(locale);
        var notesHash = ComputeStableHash(notes);
        return $"{version}|{normalizedLocale}|{notesHash}";
    }

    private static string ComputeStableHash(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash[..8]).ToLowerInvariant();
    }

    private string ResolveReleaseAnnouncementNotes(string locale)
    {
        var normalized = LocalizationConstants.NormalizeLocaleCode(locale);
        var notes = _releaseAnnouncementOptions.ResolveNotes(normalized)?.Trim();
        if (string.IsNullOrWhiteSpace(notes))
        {
            notes = _navigationPresenter.GetText("release.notes_default", normalized);
        }

        return notes;
    }

    private string BuildReleaseAnnouncementBlock(string locale, string version, string notes)
    {
        var normalized = LocalizationConstants.NormalizeLocaleCode(locale);
        var encodedVersion = WebUtility.HtmlEncode(version);
        var releaseDate = ResolveReleaseAnnouncementDate();
        var dateText = releaseDate.ToString("yyyy-MM-dd");
        var encodedDate = WebUtility.HtmlEncode(dateText);

        var title = _navigationPresenter.GetText("release.title", normalized);
        var versionLabel = _navigationPresenter.GetText("release.version", normalized);
        var dateLabel = _navigationPresenter.GetText("release.date", normalized);
        var whatsNewLabel = _navigationPresenter.GetText("release.whats_new", normalized);

        var encodedNotes = WebUtility.HtmlEncode(notes);

        return $"<b>{title}</b>{Environment.NewLine}{versionLabel}: <b>{encodedVersion}</b>{Environment.NewLine}{dateLabel}: {encodedDate}{Environment.NewLine}{whatsNewLabel}: {encodedNotes}";
    }

    private TelegramRouteResponse BuildOnboardingLanguagePickerResponse(string locale)
    {
        return new TelegramRouteResponse(
            "onboarding.language",
            BuildBilingualOnboardingText(),
            InlineKeyboard(_navigationPresenter.BuildOnboardingLanguageKeyboard(locale)));
    }

    private string BuildBilingualOnboardingText()
    {
        return string.Concat(
            _navigationPresenter.GetText("onboarding.choose_language", LocalizationConstants.EnglishLocale),
            Environment.NewLine,
            Environment.NewLine,
            _navigationPresenter.GetText("onboarding.choose_language", LocalizationConstants.UkrainianLocale));
    }

    private static TelegramSendOptions ReplyKeyboard(TelegramReplyKeyboardMarkup keyboard)
        => new(ParseMode: HtmlParseMode, ReplyMarkup: keyboard);

    private static TelegramSendOptions InlineKeyboard(TelegramInlineKeyboardMarkup keyboard)
        => new(ParseMode: HtmlParseMode, ReplyMarkup: keyboard);

    private static TelegramSendOptions EnsureHtmlParseMode(TelegramSendOptions? options)
    {
        if (options is null)
        {
            return new TelegramSendOptions(ParseMode: HtmlParseMode);
        }

        return options with { ParseMode = HtmlParseMode };
    }

    private async Task SendMainKeyboardRefreshMessageAsync(
        long chatId,
        string locale,
        int? messageThreadId,
        CancellationToken cancellationToken)
    {
        try
        {
            var text = WebUtility.HtmlEncode(_navigationPresenter.GetText("onboarding.language_saved", locale));
            var options = ReplyKeyboard(_navigationPresenter.BuildMainReplyKeyboard(locale));
            var sendResult = await _telegramBotSender.SendTextAsync(chatId, text, options, messageThreadId, cancellationToken);
            if (!sendResult.Succeeded)
            {
                _logger.LogWarning(
                    "Telegram keyboard refresh send failed. ChatId={ChatId}; Locale={Locale}; Error={Error}",
                    chatId,
                    locale,
                    sendResult.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Telegram keyboard refresh send threw exception. ChatId={ChatId}; Locale={Locale}", chatId, locale);
        }
    }

    private async Task TryAnswerCallbackQueryAsync(string? callbackQueryId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(callbackQueryId))
        {
            return;
        }

        try
        {
            var answerResult = await _telegramBotSender.AnswerCallbackQueryAsync(callbackQueryId, cancellationToken: cancellationToken);
            if (!answerResult.Succeeded)
            {
                _logger.LogWarning(
                    "Telegram callback answer failed. CallbackQueryId={CallbackQueryId}; Error={Error}",
                    callbackQueryId,
                    answerResult.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Telegram callback answer threw exception. CallbackQueryId={CallbackQueryId}", callbackQueryId);
        }
    }

    private async Task CleanupOldUpdatesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var cutoff = DateTimeOffset.UtcNow.AddHours(-25);
            await _processedUpdates.DeleteOlderThanAsync(cutoff, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Telegram processed update cleanup failed; will retry on next request.");
        }
    }

    private bool IsSecretValid()
    {
        var secret = _options.WebhookSecret;
        if (string.IsNullOrWhiteSpace(secret))
        {
            return true;
        }

        var received = Request.Headers[TelegramSecretHeader].FirstOrDefault();
        if (received is null)
        {
            return false;
        }

        var a = Encoding.UTF8.GetBytes(received);
        var b = Encoding.UTF8.GetBytes(secret);
        return CryptographicOperations.FixedTimeEquals(a, b);
    }

    private static bool IsLanguageCallback(string? callbackData)
        => !string.IsNullOrWhiteSpace(callbackData)
           && callbackData.StartsWith(CallbackDataConstants.Lang.Prefix, StringComparison.Ordinal);

    private static string? ParseLanguageCallback(string callbackData)
    {
        return callbackData switch
        {
            CallbackDataConstants.Lang.Ukrainian => LocalizationConstants.UkrainianLocale,
            CallbackDataConstants.Lang.English => LocalizationConstants.EnglishLocale,
            CallbackDataConstants.Lang.Spanish => LocalizationConstants.SpanishLocale,
            CallbackDataConstants.Lang.French => LocalizationConstants.FrenchLocale,
            CallbackDataConstants.Lang.German => LocalizationConstants.GermanLocale,
            CallbackDataConstants.Lang.Polish => LocalizationConstants.PolishLocale,
            CallbackDataConstants.Lang.Russian => LocalizationConstants.UkrainianLocale,
            _ => null
        };
    }

    private static string BuildGraphChallengeKey(ConversationScope scope)
        => string.Concat(scope.Channel, ":", scope.UserId);

    private static string BuildPendingSaveKey(ConversationScope scope)
        => string.Concat(scope.Channel, ":", scope.UserId, ":", scope.ConversationId);

    private static string BuildPendingChatActionKey(ConversationScope scope)
        => string.Concat(scope.Channel, ":", scope.UserId, ":", scope.ConversationId);

    private static string BuildPendingUrlSessionKey(ConversationScope scope)
        => string.Concat(scope.Channel, ":", scope.UserId, ":", scope.ConversationId);

    private sealed record ConfiguredDeckTarget(
        string Marker,
        string DeckFileName);

    // ── Food tracking helpers ─────────────────────────────────────────────────

    private async Task<TelegramRouteResponse> HandleFoodCallbackAsync(
        string callbackData,
        string locale,
        ConversationScope scope,
        CancellationToken cancellationToken)
    {
        // Handle meal creation confirm/cancel
        if (string.Equals(callbackData, CallbackDataConstants.Weekly.CreateConfirm, StringComparison.Ordinal))
        {
            return await HandleMealCreateConfirmAsync(scope, locale, cancellationToken);
        }

        if (string.Equals(callbackData, CallbackDataConstants.Weekly.CreateCancel, StringComparison.Ordinal))
        {
            return HandleMealCreateCancel(scope, locale);
        }

        // Handle photo food log confirm/cancel
        if (string.Equals(callbackData, CallbackDataConstants.Weekly.PhotoConfirm, StringComparison.Ordinal))
        {
            return await HandlePhotoLogConfirmAsync(scope, locale, cancellationToken);
        }

        if (string.Equals(callbackData, CallbackDataConstants.Weekly.PhotoCancel, StringComparison.Ordinal))
        {
            return HandlePhotoLogCancel(scope, locale);
        }

        var result = await _orchestrator.ProcessAsync(callbackData, TelegramChannel, locale, cancellationToken);

        // If this was a create prompt, set pending action so next text input triggers meal creation
        if (string.Equals(callbackData, CallbackDataConstants.Weekly.Create, StringComparison.Ordinal))
        {
            var pendingKey = BuildPendingChatActionKey(scope);
            _pendingStateStore.ChatActions[pendingKey] = PendingChatActionKind.MealCreation;
        }

        var keyboard = callbackData.StartsWith(CallbackDataConstants.Shop.Prefix, StringComparison.Ordinal)
            ? InlineKeyboard(_navigationPresenter.BuildShoppingKeyboard(locale))
            : InlineKeyboard(_navigationPresenter.BuildWeeklyMenuKeyboard(locale));
        return new TelegramRouteResponse(result.Intent, result.Message ?? string.Empty, keyboard);
    }

    private async Task<TelegramRouteResponse> HandleShoppingTextAsync(
        string text,
        string locale,
        CancellationToken cancellationToken)
    {
        if (_foodTrackingService is null || string.IsNullOrWhiteSpace(text))
        {
            return new TelegramRouteResponse(
                "shopping.text",
                _navigationPresenter.GetText("stub.wip", locale),
                InlineKeyboard(_navigationPresenter.BuildShoppingKeyboard(locale)));
        }

        // Parse "Name Qty Store" from free text
        var parts = text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var name = parts[0];
        var quantity = parts.Length >= 2 ? parts[1] : null;
        var store = parts.Length >= 3 ? string.Join(" ", parts.Skip(2)) : null;

        var item = await _foodTrackingService.AddGroceryItemAsync(name, quantity, store, cancellationToken);
        var qty = item.Quantity is not null ? $" × {item.Quantity}" : string.Empty;
        var st = item.Store is not null ? $" at {item.Store}" : string.Empty;

        return new TelegramRouteResponse(
            "food.shop.added",
            $"Added \"{item.Name}\"{qty}{st} to your shopping list.",
            InlineKeyboard(_navigationPresenter.BuildShoppingKeyboard(locale)));
    }

    private async Task<TelegramRouteResponse> HandleWeeklyMenuTextAsync(
        string text,
        string locale,
        ConversationScope scope,
        CancellationToken cancellationToken)
    {
        if (_foodTrackingService is null)
        {
            return new TelegramRouteResponse(
                "weekly.text",
                _navigationPresenter.GetText("stub.wip", locale),
                InlineKeyboard(_navigationPresenter.BuildWeeklyMenuKeyboard(locale)));
        }

        // Check for pending meal creation text input
        var pendingKey = BuildPendingChatActionKey(scope);
        if (_pendingStateStore.ChatActions.TryGetValue(pendingKey, out var pendingAction)
            && pendingAction == PendingChatActionKind.MealCreation)
        {
            _pendingStateStore.ChatActions.TryRemove(pendingKey, out _);
            return await HandleMealCreationTextAsync(text, scope, locale, cancellationToken);
        }

        // Check for portion calculator command: "portion <mealId> <servings>"
        var parts = text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 3
            && parts[0].Equals("portion", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(parts[1], out var portionMealId)
            && int.TryParse(parts[2], out var targetServings)
            && targetServings > 0)
        {
            var calc = await _foodTrackingService.CalculatePortionsAsync(portionMealId, targetServings, cancellationToken);
            if (calc is null)
            {
                return new TelegramRouteResponse(
                    "food.weekly.portion.not_found",
                    $"Meal with ID {portionMealId} not found.",
                    InlineKeyboard(_navigationPresenter.BuildWeeklyMenuKeyboard(locale)));
            }

            var portionSb = new System.Text.StringBuilder();
            portionSb.AppendLine($"{calc.MealName} \u2014 {calc.TargetServings} servings (\u00d7{calc.Multiplier})");
            portionSb.AppendLine();
            foreach (var i in calc.Ingredients)
            {
                var scaled = i.ScaledQuantity ?? i.OriginalQuantity ?? "\u2014";
                portionSb.AppendLine($"  \u2022 {i.Name}: {scaled}");
            }
            return new TelegramRouteResponse(
                "food.weekly.portion",
                portionSb.ToString().TrimEnd(),
                InlineKeyboard(_navigationPresenter.BuildWeeklyMenuKeyboard(locale)));
        }

        // If the user typed "N" or "N S" (meal ID + optional servings), log the meal.
        if (parts.Length >= 1
            && int.TryParse(parts[0], out var mealId)
            && mealId > 0)
        {
            var servings = parts.Length >= 2 && decimal.TryParse(parts[1], System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var s) && s > 0
                ? s
                : 1m;

            try
            {
                await _foodTrackingService.LogMealAsync(mealId, servings, notes: null, cancellationToken);
                return new TelegramRouteResponse(
                    "food.weekly.logged",
                    $"✅ Logged meal #{mealId} × {servings} serving(s).",
                    InlineKeyboard(_navigationPresenter.BuildWeeklyMenuKeyboard(locale)));
            }
            catch (InvalidOperationException)
            {
                return new TelegramRouteResponse(
                    "food.weekly.log.not_found",
                    $"⚠️ Meal with ID {mealId} not found. Press \"Log meal\" to see the list.",
                    InlineKeyboard(_navigationPresenter.BuildWeeklyMenuKeyboard(locale)));
            }
        }

        // Otherwise show the meals list.
        var meals = await _foodTrackingService.GetAllMealsAsync(cancellationToken);
        if (meals.Count == 0)
        {
            return new TelegramRouteResponse(
                "food.weekly.view",
                "No meals found. Add some meals to Notion Meal Plans first.",
                InlineKeyboard(_navigationPresenter.BuildWeeklyMenuKeyboard(locale)));
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Meal plans ({meals.Count} meals):");
        sb.AppendLine();
        foreach (var meal in meals)
        {
            var calories = meal.CaloriesPerServing.HasValue ? $" — {meal.CaloriesPerServing} kcal/serving" : string.Empty;
            sb.AppendLine($"🍽 [{meal.Id}] {meal.Name}{calories}");
        }

        return new TelegramRouteResponse(
            "food.weekly.view",
            sb.ToString().TrimEnd(),
            InlineKeyboard(_navigationPresenter.BuildWeeklyMenuKeyboard(locale)));
    }

    private async Task<TelegramRouteResponse> HandleMealCreationTextAsync(
        string mealName,
        ConversationScope scope,
        string locale,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(mealName))
        {
            return new TelegramRouteResponse(
                "food.weekly.create.empty",
                "Please type a meal name to create.",
                InlineKeyboard(_navigationPresenter.BuildWeeklyMenuKeyboard(locale)));
        }

        // Store a basic pending meal creation — the user can confirm to create it
        var pending = new PendingMealCreation(
            Name: mealName.Trim(),
            CaloriesPerServing: null,
            ProteinGrams: null,
            CarbsGrams: null,
            FatGrams: null,
            PrepTimeMinutes: null,
            DefaultServings: 2,
            Ingredients: []);

        var pendingKey = BuildPendingChatActionKey(scope);
        _pendingStateStore.MealCreations[pendingKey] = pending;

        var preview = new System.Text.StringBuilder();
        preview.AppendLine($"Create meal: \"{pending.Name}\"");
        preview.AppendLine($"Default servings: {pending.DefaultServings}");
        preview.AppendLine();
        preview.AppendLine("Confirm creation?");

        return new TelegramRouteResponse(
            "food.weekly.create.preview",
            preview.ToString().TrimEnd(),
            InlineKeyboard(_navigationPresenter.BuildMealCreateConfirmKeyboard(locale)));
    }

    private async Task<TelegramRouteResponse> HandleMealCreateConfirmAsync(
        ConversationScope scope,
        string locale,
        CancellationToken cancellationToken)
    {
        var pendingKey = BuildPendingChatActionKey(scope);
        if (!_pendingStateStore.MealCreations.TryRemove(pendingKey, out var pending))
        {
            return new TelegramRouteResponse(
                "food.weekly.create.expired",
                "No pending meal creation found. Please start again.",
                InlineKeyboard(_navigationPresenter.BuildWeeklyMenuKeyboard(locale)));
        }

        if (_foodTrackingService is null)
        {
            return new TelegramRouteResponse(
                "food.weekly.create.error",
                "Food tracking is not configured.",
                InlineKeyboard(_navigationPresenter.BuildWeeklyMenuKeyboard(locale)));
        }

        var ingredients = pending.Ingredients
            .Select(i => (i.Name, i.Quantity))
            .ToList();

        var meal = await _foodTrackingService.CreateMealAsync(
            pending.Name,
            pending.CaloriesPerServing,
            pending.ProteinGrams,
            pending.CarbsGrams,
            pending.FatGrams,
            pending.PrepTimeMinutes,
            pending.DefaultServings,
            ingredients,
            cancellationToken);

        return new TelegramRouteResponse(
            "food.weekly.create.done",
            $"Meal \"{meal.Name}\" created (ID: {meal.Id}).",
            InlineKeyboard(_navigationPresenter.BuildWeeklyMenuKeyboard(locale)));
    }

    private TelegramRouteResponse HandleMealCreateCancel(
        ConversationScope scope,
        string locale)
    {
        var pendingKey = BuildPendingChatActionKey(scope);
        _pendingStateStore.MealCreations.TryRemove(pendingKey, out _);

        return new TelegramRouteResponse(
            "food.weekly.create.cancelled",
            "Meal creation cancelled.",
            InlineKeyboard(_navigationPresenter.BuildWeeklyMenuKeyboard(locale)));
    }

    private async Task<TelegramRouteResponse> HandleWeeklyMenuPhotoAsync(
        string photoFileId,
        string locale,
        ConversationScope scope,
        CancellationToken cancellationToken)
    {
        var result = await _importSourceReader.IdentifyFoodAsync(photoFileId, cancellationToken);

        if (!result.Success || result.MealName is null || result.EstimatedCalories is null)
        {
            return new TelegramRouteResponse(
                "food.photo.failed",
                result.Error ?? "Could not identify food in the photo. Try again with a clearer image.",
                InlineKeyboard(_navigationPresenter.BuildWeeklyMenuKeyboard(locale)));
        }

        var pending = new PendingFoodPhotoLog(result.MealName, result.EstimatedCalories.Value, 1m);
        var pendingKey = BuildPendingChatActionKey(scope);
        _pendingStateStore.FoodPhotoLogs[pendingKey] = pending;

        var preview = new System.Text.StringBuilder();
        preview.AppendLine($"Identified: {pending.MealName}");
        preview.AppendLine($"Estimated calories: {pending.EstimatedCalories} kcal");
        preview.AppendLine($"Servings: {pending.Servings}");
        preview.AppendLine();
        preview.AppendLine("Log this meal?");

        return new TelegramRouteResponse(
            "food.photo.preview",
            preview.ToString().TrimEnd(),
            InlineKeyboard(_navigationPresenter.BuildFoodPhotoConfirmKeyboard(locale)));
    }

    private async Task<TelegramRouteResponse> HandlePhotoLogConfirmAsync(
        ConversationScope scope,
        string locale,
        CancellationToken cancellationToken)
    {
        var pendingKey = BuildPendingChatActionKey(scope);
        if (!_pendingStateStore.FoodPhotoLogs.TryRemove(pendingKey, out var pending))
        {
            return new TelegramRouteResponse(
                "food.photo.expired",
                "No pending photo log found. Please send a photo again.",
                InlineKeyboard(_navigationPresenter.BuildWeeklyMenuKeyboard(locale)));
        }

        if (_foodTrackingService is null)
        {
            return new TelegramRouteResponse(
                "food.photo.error",
                "Food tracking is not configured.",
                InlineKeyboard(_navigationPresenter.BuildWeeklyMenuKeyboard(locale)));
        }

        await _foodTrackingService.LogQuickMealAsync(pending.MealName, pending.EstimatedCalories, pending.Servings, cancellationToken);

        return new TelegramRouteResponse(
            "food.photo.logged",
            $"\u2705 Logged \"{pending.MealName}\" ({pending.EstimatedCalories} kcal \u00d7 {pending.Servings}).",
            InlineKeyboard(_navigationPresenter.BuildWeeklyMenuKeyboard(locale)));
    }

    private TelegramRouteResponse HandlePhotoLogCancel(
        ConversationScope scope,
        string locale)
    {
        var pendingKey = BuildPendingChatActionKey(scope);
        _pendingStateStore.FoodPhotoLogs.TryRemove(pendingKey, out _);

        return new TelegramRouteResponse(
            "food.photo.cancelled",
            "Photo log cancelled.",
            InlineKeyboard(_navigationPresenter.BuildWeeklyMenuKeyboard(locale)));
    }

    private sealed record VocabularyUrlSelectionResult(
        VocabularyUrlSelectionAction Action,
        IReadOnlyList<string> SelectedWords)
    {
        public static VocabularyUrlSelectionResult Invalid { get; } = new(VocabularyUrlSelectionAction.Invalid, []);

        public static VocabularyUrlSelectionResult Cancelled { get; } = new(VocabularyUrlSelectionAction.Cancel, []);
    }

    private enum VocabularyUrlSelectionAction
    {
        Invalid = 0,
        Select = 1,
        Cancel = 2
    }

    private sealed record OneDriveSyncSummary(
        int Completed,
        int Requeued,
        int Failed,
        int PendingAfterRun);

    private sealed record TelegramRouteResponse(
        string Intent,
        string Text,
        TelegramSendOptions? Options = null,
        bool IsHtml = false,
        string? FollowUpMainKeyboardLocale = null);
}
