using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Text.Json;
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
using LagerthaAssistant.Application.Models.AI;
using LagerthaAssistant.Application.Models.Food;
using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.Application.Navigation;
using LagerthaAssistant.Application.Services.Food;
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
    private const string InfoMarker = "ℹ️";
    private static readonly string[] WarningMarkers = ["⚠️", "⚠"];
    private const int ManualSyncBatchSize = 25;
    private const int ManualSyncMaxPasses = 5;
    private static readonly Regex UrlLikeRegex = new("^https?://", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SelectionNumberRegex = new(@"\d+", RegexOptions.Compiled);
    private static readonly Regex LeadingDecorationRegex = new("^[^\\p{L}\\p{N}]+", RegexOptions.Compiled);
    private static readonly Regex SentenceSplitRegex = new("(?<=[\\.!\\?])\\s+", RegexOptions.Compiled);
    private static readonly Regex LeadingWordSeparatorRegex = new("^[\\s:;,.!\\-–—]+", RegexOptions.Compiled);
    private static readonly Regex CyrillicRegex = new("[\\u0400-\\u04FF]", RegexOptions.Compiled);
    private static readonly Regex InventoryLeadingIdRegex = new(
        @"^\s*\[?(?<id>\d+)\]?(?<tail>.*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex InventoryTrailingOperatorRegex = new(
        @"(?<![+\-=])(?<op>[+\-=])\s*(?<value>\d+(?:[.,]\d+)?)\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex InventoryTrailingPlainValueRegex = new(
        @"(?:^|[\s:])(?<value>\d+(?:[.,]\d+)?)\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex InventoryPotentialOperationHintRegex = new(
        @"[+\-=]\s*\d",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex InventoryQuantityTokenRegex = new(
        @"^\d+(?:[.,]\d+)?(?:\s?[a-zA-Z\u0430-\u044f\u0410-\u042f\u0456\u0457\u0454\u0491\u0490]{1,8})?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex ShoppingDeleteLeadingNumberRegex = new(
        @"^\s*[^\d]*(?<number>\d+)\s*[\)\].:\-]?\s*(?<tail>.*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly string[] PrimaryPartOfSpeechMarkers = ["n", "v", "pv", "iv", "adv", "prep"];
    private static readonly string[] UrlSelectionPosOrder = ["n", "v", "adj"];
    private static readonly HashSet<string> UrlSelectAllTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "all", "всі", "усі", "todo", "todos", "tous", "alle", "wszystkie"
    };
    private static readonly HashSet<string> UrlCancelTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "cancel", "stop", "no", "ні", "cancelar", "annuler", "abbrechen", "anuluj"
    };
    private static readonly IReadOnlyDictionary<string, string> CategoryEmojis = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Beverages"] = "🍺",
        ["Canned Goods"] = "🥫",
        ["Condiments"] = "🧂",
        ["Confectionery"] = "🍬",
        ["Dairy"] = "🧀",
        ["Frozen Foods"] = "🧊",
        ["Home & Cleaning"] = "🧽",
        ["Meat & Seafood"] = "🥩",
        ["Pantry"] = "🥖",
        ["Produce"] = "🥬",
        ["Lagertha Inbox"] = "📥"
    };
    private const string DefaultUnknownInventoryCategory = "Lagertha Inbox";

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
    private readonly IAiRuntimeSettingsService _aiRuntimeSettingsService;
    private readonly ITelegramImportSourceReader _importSourceReader;
    private readonly VocabularyDeckOptions _vocabularyDeckOptions;
    private readonly IGraphAuthService _graphAuthService;
    private readonly ITelegramNavigationPresenter _navigationPresenter;
    private readonly ITelegramConversationResponseFormatter _responseFormatter;
    private readonly ITelegramBotSender _telegramBotSender;
    private readonly ITelegramProcessedUpdateRepository _processedUpdates;
    private readonly MiniAppSettingsCommitService _miniAppSettingsCommitService;
    private readonly IFoodTrackingService? _foodTrackingService;
    private readonly IUserMemoryRepository? _userMemoryRepository;
    private readonly IUnitOfWork? _unitOfWork;
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
        IAiRuntimeSettingsService aiRuntimeSettingsService,
        ITelegramImportSourceReader importSourceReader,
        VocabularyDeckOptions vocabularyDeckOptions,
        IGraphAuthService graphAuthService,
        ITelegramNavigationPresenter navigationPresenter,
        ITelegramConversationResponseFormatter responseFormatter,
        ITelegramBotSender telegramBotSender,
        ITelegramProcessedUpdateRepository processedUpdates,
        MiniAppSettingsCommitService miniAppSettingsCommitService,
        IOptions<TelegramOptions> options,
        ILogger<TelegramController> logger,
        IUserMemoryRepository? userMemoryRepository = null,
        IUnitOfWork? unitOfWork = null,
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
        _aiRuntimeSettingsService = aiRuntimeSettingsService;
        _importSourceReader = importSourceReader;
        _vocabularyDeckOptions = vocabularyDeckOptions;
        _graphAuthService = graphAuthService;
        _navigationPresenter = navigationPresenter;
        _responseFormatter = responseFormatter;
        _telegramBotSender = telegramBotSender;
        _processedUpdates = processedUpdates;
        _miniAppSettingsCommitService = miniAppSettingsCommitService;
        _userMemoryRepository = userMemoryRepository;
        _unitOfWork = unitOfWork;
        _foodTrackingService = foodTrackingService;
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
                // CancellationToken.None: Telegram requires ack within 30s regardless of request cancellation.
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

            if (!string.IsNullOrWhiteSpace(inbound.WebAppData))
            {
                if (await TryHandleMiniAppWebhookEventAsync(inbound, scope, update.UpdateId, cancellationToken))
                {
                    return Ok(new TelegramWebhookResponse(
                        Processed: true,
                        Replied: true,
                        Intent: "miniapp.settings.saved",
                        Error: null));
                }
            }

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

            if (response.SuppressChatMessage)
            {
                await _processedUpdates.MarkProcessedAsync(update.UpdateId, cancellationToken);
                await CleanupOldUpdatesAsync(CancellationToken.None);

                return Ok(new TelegramWebhookResponse(
                    Processed: true,
                    Replied: false,
                    Intent: response.Intent,
                    Error: null));
            }

            var normalizedResponseText = NormalizeMarkerSpacing(response.Text);
            var outboundText = response.IsHtml
                ? normalizedResponseText
                : WebUtility.HtmlEncode(normalizedResponseText);

            if (localeState.IsSwitched)
            {
                var switchedText = WebUtility.HtmlEncode(_navigationPresenter.GetText("locale.switched", localeState.Locale));
                outboundText = string.Concat(switchedText, Environment.NewLine, Environment.NewLine, outboundText);
            }

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
                    "onboarding.language_saved",
                    CancellationToken.None);
            }

            await _processedUpdates.MarkProcessedAsync(update.UpdateId, cancellationToken);
            // CancellationToken.None: cleanup is a best-effort background operation, must not be skipped on cancellation.
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
            // CancellationToken.None: must ack pending callback even if the request was cancelled/errored.
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
                _pendingStateStore.InventoryPhotoSessions.TryRemove(BuildPendingChatActionKey(scope), out _);
                _pendingStateStore.ShoppingDeleteSessions.TryRemove(BuildPendingShoppingDeleteKey(scope), out _);
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
                        locale,
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
                _pendingStateStore.ShoppingDeleteSessions.TryRemove(BuildPendingShoppingDeleteKey(scope), out _);
                return await BuildVocabularySectionResponseAsync(locale, cancellationToken);

            case NavigationRouteKind.MainShoppingButton:
                await _navigationStateService.SetCurrentSectionAsync(scope.Channel, scope.UserId, scope.ConversationId, NavigationSections.Inventory, cancellationToken);
                _pendingStateStore.ShoppingDeleteSessions.TryRemove(BuildPendingShoppingDeleteKey(scope), out _);
                return new TelegramRouteResponse(
                    "nav.inventory",
                    _navigationPresenter.GetText("menu.inventory.title", locale),
                    InlineKeyboard(_navigationPresenter.BuildInventoryKeyboard(locale)));

            case NavigationRouteKind.MainWeeklyMenuButton:
                await _navigationStateService.SetCurrentSectionAsync(scope.Channel, scope.UserId, scope.ConversationId, NavigationSections.WeeklyMenu, cancellationToken);
                _pendingStateStore.ShoppingDeleteSessions.TryRemove(BuildPendingShoppingDeleteKey(scope), out _);
                return new TelegramRouteResponse(
                    "nav.weekly",
                    _navigationPresenter.GetText("menu.weekly.title", locale),
                    InlineKeyboard(_navigationPresenter.BuildWeeklyMenuKeyboard(locale)));

            case NavigationRouteKind.MainSettingsButton:
                await _navigationStateService.SetCurrentSectionAsync(scope.Channel, scope.UserId, scope.ConversationId, NavigationSections.Settings, cancellationToken);
                _pendingStateStore.ShoppingDeleteSessions.TryRemove(BuildPendingShoppingDeleteKey(scope), out _);
                return await BuildSettingsEntryResponseAsync(scope, locale, cancellationToken);

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
                        locale,
                        scope.UserId,
                        scope.ConversationId,
                        cancellationToken);
                    return await BuildVocabularyTextResponseAsync(result, scope, locale, inbound.Text, cancellationToken);
                }

            case NavigationRouteKind.ShoppingText:
                return await HandleShoppingTextAsync(inbound.Text, locale, scope, cancellationToken);

            case NavigationRouteKind.InventoryText:
                return await HandleInventoryTextAsync(inbound, locale, scope, cancellationToken);

            case NavigationRouteKind.WeeklyMenuText:
                if (!string.IsNullOrWhiteSpace(inbound.PhotoFileId))
                    return await HandleWeeklyMenuPhotoAsync(inbound.PhotoFileId, locale, scope, cancellationToken);
                return await HandleWeeklyMenuTextAsync(inbound.Text, locale, scope, cancellationToken);

            case NavigationRouteKind.SettingsText:
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

                    await _navigationStateService.SetCurrentSectionAsync(scope.Channel, scope.UserId, scope.ConversationId, NavigationSections.Settings, cancellationToken);
                    return await BuildSettingsEntryResponseAsync(scope, locale, cancellationToken);
                }

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
                        locale,
                        scope.UserId,
                        scope.ConversationId,
                        cancellationToken);

                    var response = await BuildVocabularyTextResponseAsync(result, scope, locale, text, cancellationToken);
                    _pendingStateStore.ChatActions.TryRemove(pendingKey, out _);
                    return await FinalizeChatActionResponseAsync(scope, response, cancellationToken);
                }

            case PendingChatActionKind.SettingsAiApiKey:
                {
                    var text = inbound.Text?.Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        return null;
                    }

                    if (UrlCancelTokens.Contains(text))
                    {
                        _pendingStateStore.ChatActions.TryRemove(pendingKey, out _);
                        var cancelledScreen = await BuildAiSectionResponseAsync(scope, locale, cancellationToken);
                        return cancelledScreen with
                        {
                            Intent = "settings.ai.key.cancelled",
                            Text = string.Concat(
                                EnsureInfoMarker(_navigationPresenter.GetText("ai.key.cancelled", locale)),
                                Environment.NewLine,
                                Environment.NewLine,
                                cancelledScreen.Text),
                            IsHtml = true
                        };
                    }

                    var provider = await _aiRuntimeSettingsService.GetProviderAsync(scope, cancellationToken);
                    try
                    {
                        await _aiRuntimeSettingsService.SetApiKeyAsync(scope, provider, text, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "Failed to save AI API key. Channel={Channel}; UserId={UserId}; Provider={Provider}",
                            scope.Channel,
                            scope.UserId,
                            provider);

                        return new TelegramRouteResponse(
                            "settings.ai.key.failed",
                            EnsureWarningMarker(_navigationPresenter.GetText("ai.key.save_failed", locale)),
                            InlineKeyboard(_navigationPresenter.BuildAiSettingsKeyboard(locale)));
                    }

                    _pendingStateStore.ChatActions.TryRemove(pendingKey, out _);
                    var screen = await BuildAiSectionResponseAsync(scope, locale, cancellationToken);
                    return screen with
                    {
                        Intent = "settings.ai.key.saved",
                        Text = string.Concat(
                            EnsureInfoMarker(_navigationPresenter.GetText("ai.key.saved", locale, GetAiProviderDisplayName(provider))),
                            Environment.NewLine,
                            Environment.NewLine,
                            screen.Text),
                        IsHtml = true
                    };
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
                        locale,
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
                    await BuildSettingsEntryResponseAsync(scope, locale, cancellationToken),
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
            _pendingStateStore.InventoryPhotoSessions.TryRemove(BuildPendingChatActionKey(scope), out _);
            _pendingStateStore.ShoppingDeleteSessions.TryRemove(BuildPendingShoppingDeleteKey(scope), out _);
            return new TelegramRouteResponse(
                "nav.main",
                _navigationPresenter.GetText("menu.main.title", locale),
                ReplyKeyboard(_navigationPresenter.BuildMainReplyKeyboard(locale)));
        }

        if (string.Equals(callbackData, CallbackDataConstants.Nav.Weekly, StringComparison.Ordinal))
        {
            await _navigationStateService.SetCurrentSectionAsync(scope.Channel, scope.UserId, scope.ConversationId, NavigationSections.WeeklyMenu, cancellationToken);
            return new TelegramRouteResponse(
                "nav.weekly",
                _navigationPresenter.GetText("menu.weekly.title", locale),
                InlineKeyboard(_navigationPresenter.BuildWeeklyMenuKeyboard(locale)));
        }

        if (callbackData.StartsWith(CallbackDataConstants.Lang.Prefix, StringComparison.Ordinal))
        {
            return await HandleLanguageCallbackAsync(callbackData, scope, locale, currentSection, cancellationToken);
        }

        if (callbackData.StartsWith(CallbackDataConstants.Settings.Prefix, StringComparison.Ordinal))
        {
            return await HandleSettingsCallbackAsync(callbackData, scope, locale, cancellationToken);
        }

        if (callbackData.StartsWith(CallbackDataConstants.Ai.Prefix, StringComparison.Ordinal))
        {
            return await HandleAiCallbackAsync(callbackData, scope, locale, cancellationToken);
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

        if (callbackData.StartsWith(CallbackDataConstants.Food.Prefix, StringComparison.Ordinal))
        {
            return await HandleFoodMenuCallbackAsync(callbackData, locale, scope, cancellationToken);
        }

        if (callbackData.StartsWith(CallbackDataConstants.Inventory.Prefix, StringComparison.Ordinal))
        {
            await _navigationStateService.SetCurrentSectionAsync(scope.Channel, scope.UserId, scope.ConversationId, NavigationSections.Inventory, cancellationToken);
            return await HandleInventoryCallbackAsync(callbackData, locale, scope, cancellationToken);
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
            CallbackDataConstants.Settings.Legacy => await BuildSettingsSectionResponseAsync(scope, locale, cancellationToken),
            CallbackDataConstants.Settings.Language => new TelegramRouteResponse(
                "settings.language",
                _navigationPresenter.GetText("language.current", locale, _navigationPresenter.GetLanguageDisplayName(locale)),
                InlineKeyboard(_navigationPresenter.BuildSettingsLanguageKeyboard(locale)),
                IsHtml: true),
            CallbackDataConstants.Settings.SaveMode => await BuildSaveModeResponseAsync(scope, locale, cancellationToken),
            CallbackDataConstants.Settings.Ai => await BuildAiSectionResponseAsync(scope, locale, cancellationToken),
            CallbackDataConstants.Settings.OneDrive => await BuildOneDriveResponseAsync(scope, locale, includeCheckStatusButton: false, cancellationToken),
            CallbackDataConstants.Settings.Notion => BuildNotionSectionResponse(locale),
            CallbackDataConstants.Settings.Back => await BuildSettingsSectionResponseAsync(scope, locale, cancellationToken),
            _ => await BuildSettingsSectionResponseAsync(scope, locale, cancellationToken)
        };
    }

    private async Task<TelegramRouteResponse> HandleAiCallbackAsync(
        string callbackData,
        ConversationScope scope,
        string locale,
        CancellationToken cancellationToken)
    {
        await _navigationStateService.SetCurrentSectionAsync(
            scope.Channel,
            scope.UserId,
            scope.ConversationId,
            NavigationSections.Settings,
            cancellationToken);

        if (string.Equals(callbackData, CallbackDataConstants.Ai.Back, StringComparison.Ordinal))
        {
            return await BuildSettingsSectionResponseAsync(scope, locale, cancellationToken);
        }

        if (string.Equals(callbackData, CallbackDataConstants.Ai.Provider, StringComparison.Ordinal))
        {
            var provider = await _aiRuntimeSettingsService.GetProviderAsync(scope, cancellationToken);
            var title = _navigationPresenter.GetText("ai.provider.title", locale, GetAiProviderDisplayName(provider));
            return new TelegramRouteResponse(
                "settings.ai.provider",
                title,
                InlineKeyboard(_navigationPresenter.BuildAiProviderKeyboard(locale)));
        }

        if (string.Equals(callbackData, CallbackDataConstants.Ai.Model, StringComparison.Ordinal))
        {
            var provider = await _aiRuntimeSettingsService.GetProviderAsync(scope, cancellationToken);
            var currentModel = await _aiRuntimeSettingsService.GetModelAsync(scope, provider, cancellationToken);
            var models = _aiRuntimeSettingsService.GetSupportedModels(provider);
            var text = string.Join(
                Environment.NewLine,
                new[]
                {
                    _navigationPresenter.GetText("ai.model.title", locale, GetAiProviderDisplayName(provider)),
                    string.Empty,
                    $"• <b>{WebUtility.HtmlEncode(_navigationPresenter.GetText("ai.model.current", locale))}:</b> <code>{WebUtility.HtmlEncode(currentModel)}</code>"
                });

            return new TelegramRouteResponse(
                "settings.ai.model",
                text,
                InlineKeyboard(_navigationPresenter.BuildAiModelKeyboard(locale, models)),
                IsHtml: true);
        }

        if (string.Equals(callbackData, CallbackDataConstants.Ai.KeySet, StringComparison.Ordinal))
        {
            _pendingStateStore.ChatActions[BuildPendingChatActionKey(scope)] = PendingChatActionKind.SettingsAiApiKey;
            return new TelegramRouteResponse(
                "settings.ai.key.awaiting",
                EnsureQuestionMarker(_navigationPresenter.GetText("ai.key.prompt", locale)),
                InlineKeyboard(_navigationPresenter.BuildAiSettingsKeyboard(locale)));
        }

        if (string.Equals(callbackData, CallbackDataConstants.Ai.KeyRemove, StringComparison.Ordinal))
        {
            var provider = await _aiRuntimeSettingsService.GetProviderAsync(scope, cancellationToken);
            await _aiRuntimeSettingsService.RemoveApiKeyAsync(scope, provider, cancellationToken);
            var screen = await BuildAiSectionResponseAsync(scope, locale, cancellationToken);
            return screen with
            {
                Intent = "settings.ai.key.removed",
                Text = string.Concat(
                    EnsureInfoMarker(_navigationPresenter.GetText("ai.key.removed", locale, GetAiProviderDisplayName(provider))),
                    Environment.NewLine,
                    Environment.NewLine,
                    screen.Text),
                IsHtml = true
            };
        }

        if (callbackData.StartsWith(CallbackDataConstants.Ai.ProviderSetPrefix, StringComparison.Ordinal))
        {
            var requestedProvider = callbackData[CallbackDataConstants.Ai.ProviderSetPrefix.Length..];
            if (!_aiRuntimeSettingsService.TryNormalizeProvider(requestedProvider, out var provider))
            {
                return await BuildAiSectionResponseAsync(scope, locale, cancellationToken);
            }

            await _aiRuntimeSettingsService.SetProviderAsync(scope, provider, cancellationToken);
            var screen = await BuildAiSectionResponseAsync(scope, locale, cancellationToken);
            return screen with
            {
                Intent = "settings.ai.provider.changed",
                Text = string.Concat(
                    EnsureInfoMarker(_navigationPresenter.GetText("ai.provider.changed", locale, GetAiProviderDisplayName(provider))),
                    Environment.NewLine,
                    Environment.NewLine,
                    screen.Text),
                IsHtml = true
            };
        }

        if (callbackData.StartsWith(CallbackDataConstants.Ai.ModelSetPrefix, StringComparison.Ordinal))
        {
            var model = callbackData[CallbackDataConstants.Ai.ModelSetPrefix.Length..].Trim();
            var provider = await _aiRuntimeSettingsService.GetProviderAsync(scope, cancellationToken);
            if (string.IsNullOrWhiteSpace(model))
            {
                return await BuildAiSectionResponseAsync(scope, locale, cancellationToken);
            }

            await _aiRuntimeSettingsService.SetModelAsync(scope, provider, model, cancellationToken);
            var screen = await BuildAiSectionResponseAsync(scope, locale, cancellationToken);
            return screen with
            {
                Intent = "settings.ai.model.changed",
                Text = string.Concat(
                    EnsureInfoMarker(_navigationPresenter.GetText("ai.model.changed", locale, model)),
                    Environment.NewLine,
                    Environment.NewLine,
                    screen.Text),
                IsHtml = true
            };
        }

        return await BuildAiSectionResponseAsync(scope, locale, cancellationToken);
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
            locale,
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
                string.Empty,
                SuppressChatMessage: true);
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
                string.Empty,
                SuppressChatMessage: true);
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
        var aiRuntime = await _aiRuntimeSettingsService.ResolveAsync(scope, cancellationToken);
        var storageMode = VocabularyStorageMode.Graph;
        _storageModeProvider.SetMode(storageMode);
        var graphStatus = await _graphAuthService.GetStatusAsync(cancellationToken);
        var languageLabel = StripLeadingDecorations(_navigationPresenter.GetText("settings.language", locale));
        var saveModeLabel = StripLeadingDecorations(_navigationPresenter.GetText("settings.save_mode", locale));
        var aiLabel = StripLeadingDecorations(_navigationPresenter.GetText("settings.ai", locale));
        var storageModeLabel = StripLeadingDecorations(_navigationPresenter.GetText("settings.storage_mode", locale));
        var oneDriveLabel = StripLeadingDecorations(_navigationPresenter.GetText("settings.onedrive", locale));
        var notionLabel = StripLeadingDecorations(_navigationPresenter.GetText("settings.notion", locale));
        var notionStatusKey = ResolveNotionSettingsStatusKey();
        var aiStatus = _navigationPresenter.GetText(
            aiRuntime.ApiKeySource switch
            {
                AiApiKeySource.Stored => "ai.key.status.stored",
                AiApiKeySource.Environment => "ai.key.status.environment",
                _ => "ai.key.status.missing"
            },
            locale);
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
            $"• <b>{WebUtility.HtmlEncode(aiLabel)}:</b> {WebUtility.HtmlEncode(GetAiProviderDisplayName(aiRuntime.Provider))} / <code>{WebUtility.HtmlEncode(aiRuntime.Model)}</code> ({WebUtility.HtmlEncode(aiStatus)})",
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

    private async Task<TelegramRouteResponse> BuildSettingsEntryResponseAsync(
        ConversationScope scope,
        string locale,
        CancellationToken cancellationToken)
    {
        await _navigationStateService.SetCurrentSectionAsync(
            scope.Channel,
            scope.UserId,
            scope.ConversationId,
            NavigationSections.Settings,
            cancellationToken);

        if (!_navigationPresenter.CanLaunchSettingsMiniApp)
        {
            return await BuildSettingsSectionResponseAsync(scope, locale, cancellationToken);
        }

        var title = _navigationPresenter.GetText("settings.launch.title", locale);
        var body = WebUtility.HtmlEncode(_navigationPresenter.GetText("settings.launch.body", locale));
        var text = string.Concat(title, Environment.NewLine, Environment.NewLine, body);

        return new TelegramRouteResponse(
            "settings.launch",
            text,
            InlineKeyboard(_navigationPresenter.BuildSettingsLaunchKeyboard(locale)),
            IsHtml: true);
    }

    private async Task<TelegramRouteResponse> BuildAiSectionResponseAsync(
        ConversationScope scope,
        string locale,
        CancellationToken cancellationToken)
    {
        var provider = await _aiRuntimeSettingsService.GetProviderAsync(scope, cancellationToken);
        var model = await _aiRuntimeSettingsService.GetModelAsync(scope, provider, cancellationToken);
        var runtime = await _aiRuntimeSettingsService.ResolveAsync(scope, cancellationToken);
        var providerLabel = StripLeadingDecorations(_navigationPresenter.GetText("ai.provider.label", locale));
        var modelLabel = StripLeadingDecorations(_navigationPresenter.GetText("ai.model.label", locale));
        var keyLabel = StripLeadingDecorations(_navigationPresenter.GetText("ai.key.label", locale));
        var keyStatus = _navigationPresenter.GetText(
            runtime.ApiKeySource switch
            {
                AiApiKeySource.Stored => "ai.key.status.stored",
                AiApiKeySource.Environment => "ai.key.status.environment",
                _ => "ai.key.status.missing"
            },
            locale);

        var text = string.Join(
            Environment.NewLine,
            new[]
            {
                _navigationPresenter.GetText("ai.title", locale),
                string.Empty,
                $"• <b>{WebUtility.HtmlEncode(providerLabel)}:</b> {WebUtility.HtmlEncode(GetAiProviderDisplayName(provider))}",
                $"• <b>{WebUtility.HtmlEncode(modelLabel)}:</b> <code>{WebUtility.HtmlEncode(model)}</code>",
                $"• <b>{WebUtility.HtmlEncode(keyLabel)}:</b> {WebUtility.HtmlEncode(keyStatus)}"
            });

        return new TelegramRouteResponse(
            "settings.ai",
            text,
            InlineKeyboard(_navigationPresenter.BuildAiSettingsKeyboard(locale)),
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

    private static string GetAiProviderDisplayName(string provider)
    {
        return provider switch
        {
            AiProviderConstants.Claude => "Claude",
            _ => "OpenAI"
        };
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
        return EnsureSingleMarker(value, QuestionMarker, preserveExistingMarker: true);
    }

    private static string EnsureInfoMarker(string value)
    {
        return EnsureSingleMarker(value, InfoMarker, preserveExistingMarker: true);
    }

    private static string GetWarningMarker()
    {
        return WarningMarkers[0];
    }

    private static string EnsureWarningMarker(string value)
    {
        return EnsureSingleMarker(value, GetWarningMarker(), preserveExistingMarker: false);
    }

    private static string EnsureSingleMarker(
        string value,
        string preferredMarker,
        bool preserveExistingMarker)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var cleanedContent = StripLeadingStatusMarkers(value, out var existingMarker);
        if (string.IsNullOrWhiteSpace(cleanedContent))
        {
            cleanedContent = value.Trim();
        }

        var marker = preserveExistingMarker
            ? existingMarker ?? preferredMarker
            : preferredMarker;

        return string.Concat(marker, " ", cleanedContent).Trim();
    }

    private static string StripLeadingStatusMarkers(string value, out string? existingMarker)
    {
        existingMarker = null;
        var current = value.TrimStart();
        var consumed = false;

        while (true)
        {
            var marker = GetLeadingStatusMarker(current);
            if (marker is null)
            {
                break;
            }

            consumed = true;
            if (WarningMarkers.Any(w => string.Equals(w, marker, StringComparison.Ordinal)))
            {
                existingMarker = GetWarningMarker();
            }
            else if (existingMarker is null)
            {
                existingMarker = marker;
            }

            current = current[marker.Length..].TrimStart();
        }

        if (!consumed)
        {
            return value.Trim();
        }

        return current.Trim();
    }

    private static string? GetLeadingStatusMarker(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var warningMarker = WarningMarkers.FirstOrDefault(marker => value.StartsWith(marker, StringComparison.Ordinal));
        if (warningMarker is not null)
        {
            return warningMarker;
        }

        if (value.StartsWith(QuestionMarker, StringComparison.Ordinal))
        {
            return QuestionMarker;
        }

        if (value.StartsWith(InfoMarker, StringComparison.Ordinal))
        {
            return InfoMarker;
        }

        return null;
    }

    private static string NormalizeMarkerSpacing(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Replace("\r\n", "\n");
        var lines = normalized.Split('\n');
        var output = new List<string>(lines.Length + 4);
        var previousNonEmptyWasMarker = false;

        foreach (var line in lines)
        {
            var isEmpty = string.IsNullOrWhiteSpace(line);
            var isMarker = IsStatusMarkerLine(line);

            if (isMarker && previousNonEmptyWasMarker && (output.Count == 0 || output[^1].Length > 0))
            {
                output.Add(string.Empty);
            }

            output.Add(line);

            if (isEmpty)
            {
                previousNonEmptyWasMarker = false;
                continue;
            }

            previousNonEmptyWasMarker = isMarker;
        }

        return string.Join(Environment.NewLine, output);
    }

    private static bool IsStatusMarkerLine(string line)
    {
        var trimmed = line.TrimStart();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return false;
        }

        if (WarningMarkers.Any(marker => trimmed.StartsWith(marker, StringComparison.Ordinal)))
        {
            return true;
        }

        return trimmed.StartsWith(QuestionMarker, StringComparison.Ordinal)
            || trimmed.StartsWith(InfoMarker, StringComparison.Ordinal);
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

        var normalizedText = NormalizeMarkerSpacing(text);
        var sendResult = await _telegramBotSender.SendTextAsync(
            chatId,
            WebUtility.HtmlEncode(normalizedText),
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
        string textKey,
        CancellationToken cancellationToken)
    {
        try
        {
            var text = WebUtility.HtmlEncode(_navigationPresenter.GetText(textKey, locale));
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

    private async Task<bool> TryHandleMiniAppWebhookEventAsync(
        TelegramInboundMessage inbound,
        ConversationScope scope,
        long updateId,
        CancellationToken cancellationToken)
    {
        if (TryParseMiniAppSettingsCommitEvent(inbound.WebAppData, out var commitRequest))
        {
            var commitLocale = LocalizationConstants.NormalizeLocaleCode(commitRequest.Locale);
            var commitResult = await _miniAppSettingsCommitService.CommitAsync(
                scope,
                commitRequest with
                {
                    Channel = scope.Channel,
                    UserId = scope.UserId,
                    ConversationId = scope.ConversationId
                },
                cancellationToken);

            if (!commitResult.Succeeded || commitResult.Response is null)
            {
                await SendMiniAppCommitFailureMessageAsync(
                    inbound.ChatId,
                    commitLocale,
                    inbound.MessageThreadId,
                    commitResult.ErrorMessage,
                    CancellationToken.None);

                await _processedUpdates.MarkProcessedAsync(updateId, cancellationToken);
                await CleanupOldUpdatesAsync(CancellationToken.None);
                return true;
            }

            await _navigationStateService.SetCurrentSectionAsync(
                scope.Channel,
                scope.UserId,
                scope.ConversationId,
                NavigationSections.Main,
                cancellationToken);

            await SendMainKeyboardRefreshMessageAsync(
                inbound.ChatId,
                commitResult.Response.Locale,
                inbound.MessageThreadId,
                "menu.main.title",
                CancellationToken.None);

            await _processedUpdates.MarkProcessedAsync(updateId, cancellationToken);
            await CleanupOldUpdatesAsync(CancellationToken.None);
            return true;
        }
        if (!TryParseMiniAppSettingsSavedEvent(inbound.WebAppData, out var requestedLocale))
        {
            return false;
        }

        var storedLocale = await _userLocaleStateService.GetStoredLocaleAsync(scope.Channel, scope.UserId, cancellationToken);
        var effectiveLocale = !string.IsNullOrWhiteSpace(storedLocale)
            ? LocalizationConstants.NormalizeLocaleCode(storedLocale)
            : LocalizationConstants.NormalizeLocaleCode(requestedLocale ?? inbound.LanguageCode);

        await _navigationStateService.SetCurrentSectionAsync(
            scope.Channel,
            scope.UserId,
            scope.ConversationId,
            NavigationSections.Main,
            cancellationToken);

        await SendMainKeyboardRefreshMessageAsync(
            inbound.ChatId,
            effectiveLocale,
            inbound.MessageThreadId,
            "menu.main.title",
            CancellationToken.None);

        await _processedUpdates.MarkProcessedAsync(updateId, cancellationToken);
        await CleanupOldUpdatesAsync(CancellationToken.None);

        return true;
    }

    private async Task SendMiniAppCommitFailureMessageAsync(
        long chatId,
        string locale,
        int? messageThreadId,
        string? errorMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            var prefix = locale == LocalizationConstants.UkrainianLocale
                ? "Не вдалося застосувати налаштування Mini App."
                : "Failed to apply Mini App settings.";

            var text = string.IsNullOrWhiteSpace(errorMessage)
                ? prefix
                : string.Concat(prefix, Environment.NewLine, Environment.NewLine, errorMessage.Trim());

            var options = ReplyKeyboard(_navigationPresenter.BuildMainReplyKeyboard(locale));
            var sendResult = await _telegramBotSender.SendTextAsync(
                chatId,
                WebUtility.HtmlEncode(text),
                options,
                messageThreadId,
                cancellationToken);

            if (!sendResult.Succeeded)
            {
                _logger.LogWarning(
                    "Telegram Mini App commit failure message send failed. ChatId={ChatId}; Locale={Locale}; Error={Error}",
                    chatId,
                    locale,
                    sendResult.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Telegram Mini App commit failure message threw exception. ChatId={ChatId}; Locale={Locale}", chatId, locale);
        }
    }
    private static bool TryParseMiniAppSettingsSavedEvent(string? rawData, out string? locale)
    {
        locale = null;

        if (string.IsNullOrWhiteSpace(rawData))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(rawData);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!root.TryGetProperty("type", out var typeElement)
                || typeElement.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            var eventType = typeElement.GetString();
            if (!string.Equals(eventType, "settings_saved", StringComparison.Ordinal))
            {
                return false;
            }

            if (root.TryGetProperty("locale", out var localeElement) && localeElement.ValueKind == JsonValueKind.String)
            {
                locale = localeElement.GetString();
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryParseMiniAppSettingsCommitEvent(
        string? rawData,
        out MiniAppSettingsCommitRequest request)
    {
        request = default!;

        if (string.IsNullOrWhiteSpace(rawData))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(rawData);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!root.TryGetProperty("type", out var typeElement)
                || typeElement.ValueKind != JsonValueKind.String
                || !string.Equals(typeElement.GetString(), "settings_commit", StringComparison.Ordinal))
            {
                return false;
            }

            if (!TryReadRequiredString(root, "locale", out var locale)
                || !TryReadRequiredString(root, "saveMode", out var saveMode)
                || !TryReadRequiredString(root, "storageMode", out var storageMode)
                || !TryReadRequiredString(root, "aiProvider", out var aiProvider)
                || !TryReadRequiredString(root, "aiModel", out var aiModel))
            {
                return false;
            }

            var apiKey = root.TryGetProperty("apiKey", out var apiKeyElement) && apiKeyElement.ValueKind == JsonValueKind.String
                ? apiKeyElement.GetString()
                : null;

            var removeStoredKey = root.TryGetProperty("removeStoredKey", out var removeStoredKeyElement)
                && removeStoredKeyElement.ValueKind == JsonValueKind.True;

            request = new MiniAppSettingsCommitRequest(
                Locale: locale,
                SaveMode: saveMode,
                StorageMode: storageMode,
                AiProvider: aiProvider,
                AiModel: aiModel,
                ApiKey: apiKey,
                RemoveStoredKey: removeStoredKey);

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryReadRequiredString(JsonElement root, string propertyName, out string value)
    {
        value = string.Empty;

        if (!root.TryGetProperty(propertyName, out var element)
            || element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var raw = element.GetString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        value = raw.Trim();
        return true;
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
            // Fail-secure: reject all requests when secret is not configured.
            // Set TelegramOptions:WebhookSecret in Railway variables.
            return false;
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

    private static string BuildPendingShoppingDeleteKey(ConversationScope scope)
        => string.Concat(scope.Channel, ":", scope.UserId, ":", scope.ConversationId);

    private sealed record ConfiguredDeckTarget(
        string Marker,
        string DeckFileName);

    // -- Food tracking helpers -------------------------------------------------

    private async Task<TelegramRouteResponse> HandleFoodMenuCallbackAsync(
        string callbackData,
        string locale,
        ConversationScope scope,
        CancellationToken cancellationToken)
    {
        if (string.Equals(callbackData, CallbackDataConstants.Food.Inventory, StringComparison.Ordinal))
        {
            await _navigationStateService.SetCurrentSectionAsync(scope.Channel, scope.UserId, scope.ConversationId, NavigationSections.Inventory, cancellationToken);
            return new TelegramRouteResponse(
                "nav.inventory",
                _navigationPresenter.GetText("menu.inventory.title", locale),
                InlineKeyboard(_navigationPresenter.BuildInventoryKeyboard(locale)));
        }

        if (string.Equals(callbackData, CallbackDataConstants.Food.Shopping, StringComparison.Ordinal))
        {
            await _navigationStateService.SetCurrentSectionAsync(scope.Channel, scope.UserId, scope.ConversationId, NavigationSections.Shopping, cancellationToken);
            return new TelegramRouteResponse(
                "nav.shopping",
                _navigationPresenter.GetText("menu.shopping.title", locale),
                InlineKeyboard(_navigationPresenter.BuildShoppingKeyboard(locale)));
        }

        await _navigationStateService.SetCurrentSectionAsync(scope.Channel, scope.UserId, scope.ConversationId, NavigationSections.Main, cancellationToken);
        return new TelegramRouteResponse(
            "nav.food",
            _navigationPresenter.GetText("menu.food.title", locale),
            InlineKeyboard(_navigationPresenter.BuildFoodMenuKeyboard(locale)));
    }

    private async Task<TelegramRouteResponse> HandleInventoryCallbackAsync(
        string callbackData,
        string locale,
        ConversationScope scope,
        CancellationToken cancellationToken)
    {
        if (_foodTrackingService is null)
        {
            return new TelegramRouteResponse(
                "inventory.unavailable",
                _navigationPresenter.GetText("stub.wip", locale),
                InlineKeyboard(_navigationPresenter.BuildInventoryKeyboard(locale)));
        }

        if (string.Equals(callbackData, CallbackDataConstants.Inventory.List, StringComparison.Ordinal)
            || string.Equals(callbackData, CallbackDataConstants.Inventory.ListAvailable, StringComparison.Ordinal)
            || string.Equals(callbackData, CallbackDataConstants.Inventory.ListMissing, StringComparison.Ordinal))
        {
            var items = await _foodTrackingService.GetAllInventoryAsync(0, cancellationToken);
            if (items.Count == 0)
            {
                return new TelegramRouteResponse(
                    "inventory.list.empty",
                    _navigationPresenter.GetText("inventory.empty", locale),
                    InlineKeyboard(_navigationPresenter.BuildInventoryKeyboard(locale)));
            }

            var showMissing = string.Equals(callbackData, CallbackDataConstants.Inventory.ListMissing, StringComparison.Ordinal);
            return new TelegramRouteResponse(
                "inventory.list",
                BuildInventoryListText(items, locale, showMissing),
                InlineKeyboard(BuildInventoryListKeyboard(locale, showMissing)));
        }

        if (string.Equals(callbackData, CallbackDataConstants.Inventory.Search, StringComparison.Ordinal))
        {
            var pendingKey = BuildPendingChatActionKey(scope);
            _pendingStateStore.ChatActions[pendingKey] = PendingChatActionKind.InventorySearch;
            return new TelegramRouteResponse(
                "inventory.search.prompt",
                $"{QuestionMarker} {_navigationPresenter.GetText("inventory.search.prompt", locale)}",
                InlineKeyboard(_navigationPresenter.BuildInventoryKeyboard(locale)));
        }

        if (string.Equals(callbackData, CallbackDataConstants.Inventory.Manage, StringComparison.Ordinal))
        {
            return new TelegramRouteResponse(
                "nav.inventory.manage",
                _navigationPresenter.GetText("menu.inventory.manage", locale),
                InlineKeyboard(_navigationPresenter.BuildInventoryManageKeyboard(locale)));
        }

        if (string.Equals(callbackData, CallbackDataConstants.Inventory.Move, StringComparison.Ordinal))
        {
            return new TelegramRouteResponse(
                "nav.inventory.move",
                _navigationPresenter.GetText("menu.inventory.move", locale),
                InlineKeyboard(_navigationPresenter.BuildInventoryMoveKeyboard(locale)));
        }

        if (string.Equals(callbackData, CallbackDataConstants.Inventory.Add, StringComparison.Ordinal))
        {
            return new TelegramRouteResponse(
                "inventory.add.prompt",
                _navigationPresenter.GetText("stub.wip", locale),
                InlineKeyboard(_navigationPresenter.BuildInventoryKeyboard(locale)));
        }

        if (string.Equals(callbackData, CallbackDataConstants.Inventory.Stats, StringComparison.Ordinal))
        {
            var stats = await _foodTrackingService.GetInventoryStatsAsync(cancellationToken);
            var statsSb = new StringBuilder();
            statsSb.AppendLine(_navigationPresenter.GetText("inventory.stats.title", locale));
            statsSb.AppendLine();
            statsSb.AppendLine(_navigationPresenter.GetText("inventory.stats.total_items", locale, stats.TotalItems));
            statsSb.AppendLine(_navigationPresenter.GetText("inventory.stats.with_current", locale, stats.WithCurrentQuantity));
            statsSb.AppendLine(_navigationPresenter.GetText("inventory.stats.with_min", locale, stats.WithMinQuantity));
            statsSb.AppendLine(_navigationPresenter.GetText("inventory.stats.low_stock", locale, stats.LowStockItems));
            statsSb.AppendLine(_navigationPresenter.GetText("inventory.stats.total_current", locale, stats.TotalCurrentQuantity));

            return new TelegramRouteResponse(
                "inventory.stats",
                statsSb.ToString().TrimEnd(),
                InlineKeyboard(_navigationPresenter.BuildInventoryKeyboard(locale)));
        }

        if (string.Equals(callbackData, CallbackDataConstants.Inventory.Adjust, StringComparison.Ordinal))
        {
            var pendingKey = BuildPendingChatActionKey(scope);
            _pendingStateStore.ChatActions[pendingKey] = PendingChatActionKind.InventoryAdjustQuantity;

            var prompt = string.Join(
                Environment.NewLine,
                EnsureQuestionMarker(_navigationPresenter.GetText("inventory.adjust.prompt", locale)),
                EnsureInfoMarker(_navigationPresenter.GetText("inventory.adjust.hint", locale)));
            return new TelegramRouteResponse(
                "inventory.adjust.prompt",
                prompt,
                InlineKeyboard(_navigationPresenter.BuildInventoryKeyboard(locale)));
        }

        if (string.Equals(callbackData, CallbackDataConstants.Inventory.Min, StringComparison.Ordinal))
        {
            var pendingKey = BuildPendingChatActionKey(scope);
            _pendingStateStore.ChatActions[pendingKey] = PendingChatActionKind.InventorySetMinQuantity;

            var prompt = string.Join(
                Environment.NewLine,
                EnsureQuestionMarker(_navigationPresenter.GetText("inventory.min.prompt", locale)),
                EnsureInfoMarker(_navigationPresenter.GetText("inventory.min.hint", locale)));
            return new TelegramRouteResponse(
                "inventory.min.prompt",
                prompt,
                InlineKeyboard(_navigationPresenter.BuildInventoryKeyboard(locale)));
        }

        if (string.Equals(callbackData, CallbackDataConstants.Inventory.ResetStock, StringComparison.Ordinal))
        {
            return new TelegramRouteResponse(
                "inventory.reset_stock.prompt",
                EnsureQuestionMarker(_navigationPresenter.GetText("inventory.reset_stock.prompt", locale)),
                InlineKeyboard(_navigationPresenter.BuildInventoryResetStockConfirmationKeyboard(locale)));
        }

        if (string.Equals(callbackData, CallbackDataConstants.Inventory.ResetStockConfirm, StringComparison.Ordinal))
        {
            var updated = await _foodTrackingService.ResetAllInventoryCurrentQuantitiesAsync(cancellationToken);
            return new TelegramRouteResponse(
                "inventory.reset_stock.done",
                EnsureInfoMarker(_navigationPresenter.GetText("inventory.reset_stock.done", locale, updated)),
                InlineKeyboard(_navigationPresenter.BuildInventoryKeyboard(locale)));
        }

        if (string.Equals(callbackData, CallbackDataConstants.Inventory.PhotoRestock, StringComparison.Ordinal)
            || string.Equals(callbackData, CallbackDataConstants.Inventory.PhotoConsume, StringComparison.Ordinal))
        {
            var mode = string.Equals(callbackData, CallbackDataConstants.Inventory.PhotoConsume, StringComparison.Ordinal)
                ? TelegramInventoryPhotoMode.Consumption
                : TelegramInventoryPhotoMode.Restock;

            var pendingKey = BuildPendingChatActionKey(scope);
            _pendingStateStore.InventoryPhotoSessions[pendingKey] = new PendingInventoryPhotoSession(mode, [], []);
            _pendingStateStore.ChatActions[pendingKey] = PendingChatActionKind.InventoryPhotoAwaitingImage;

            var promptKey = mode == TelegramInventoryPhotoMode.Consumption
                ? "inventory.photo.awaiting_consume"
                : "inventory.photo.awaiting_restock";

            return new TelegramRouteResponse(
                "inventory.photo.awaiting_image",
                EnsureQuestionMarker(_navigationPresenter.GetText(promptKey, locale)),
                InlineKeyboard(_navigationPresenter.BuildInventoryKeyboard(locale)));
        }

        if (string.Equals(callbackData, CallbackDataConstants.Inventory.PhotoSelect, StringComparison.Ordinal))
        {
            var pendingKey = BuildPendingChatActionKey(scope);
            if (!_pendingStateStore.InventoryPhotoSessions.ContainsKey(pendingKey))
            {
                return new TelegramRouteResponse(
                    "inventory.photo.expired",
                    EnsureWarningMarker(_navigationPresenter.GetText("inventory.photo.expired", locale)),
                    InlineKeyboard(_navigationPresenter.BuildInventoryKeyboard(locale)));
            }

            return new TelegramRouteResponse(
                "inventory.photo.select.prompt",
                EnsureQuestionMarker(_navigationPresenter.GetText("inventory.photo.select.prompt", locale)),
                InlineKeyboard(_navigationPresenter.BuildInventoryPhotoConfirmKeyboard(locale)));
        }

        if (string.Equals(callbackData, CallbackDataConstants.Inventory.PhotoCancel, StringComparison.Ordinal))
        {
            var pendingKey = BuildPendingChatActionKey(scope);
            _pendingStateStore.ChatActions.TryRemove(pendingKey, out _);
            _pendingStateStore.InventoryPhotoSessions.TryRemove(pendingKey, out _);

            return new TelegramRouteResponse(
                "inventory.photo.cancelled",
                EnsureInfoMarker(_navigationPresenter.GetText("inventory.photo.cancelled", locale)),
                InlineKeyboard(_navigationPresenter.BuildInventoryKeyboard(locale)));
        }

        if (string.Equals(callbackData, CallbackDataConstants.Inventory.PhotoApplyAll, StringComparison.Ordinal))
        {
            var pendingKey = BuildPendingChatActionKey(scope);
            if (!_pendingStateStore.InventoryPhotoSessions.TryGetValue(pendingKey, out var session)
                || session.Candidates.Count == 0)
            {
                _pendingStateStore.ChatActions.TryRemove(pendingKey, out _);
                return new TelegramRouteResponse(
                    "inventory.photo.expired",
                    EnsureWarningMarker(_navigationPresenter.GetText("inventory.photo.expired", locale)),
                    InlineKeyboard(_navigationPresenter.BuildInventoryKeyboard(locale)));
            }

            return await ApplyInventoryPhotoCandidatesAsync(scope, locale, pendingKey, session, session.Candidates, cancellationToken);
        }

        if (string.Equals(callbackData, CallbackDataConstants.Inventory.Suggest, StringComparison.Ordinal))
        {
            var allInventoryItems = await _foodTrackingService.GetAllInventoryAsync(0, cancellationToken);
            var withMinQuantity = allInventoryItems
                .Where(item => item.MinQuantity.HasValue)
                .ToList();

            var lowStock = withMinQuantity
                .Where(item => item.CurrentQuantity.HasValue && item.CurrentQuantity.Value < item.MinQuantity!.Value)
                .ToList();

            if (lowStock.Count == 0)
            {
                if (withMinQuantity.Count == 0)
                {
                    return new TelegramRouteResponse(
                        "inventory.suggest.empty",
                        EnsureInfoMarker(_navigationPresenter.GetText("inventory.suggest.empty", locale)),
                        InlineKeyboard(_navigationPresenter.BuildInventoryKeyboard(locale)));
                }

                var withoutCurrentQuantity = withMinQuantity.Count(item => !item.CurrentQuantity.HasValue);
                var messageKey = withoutCurrentQuantity > 0
                    ? "inventory.suggest.missing_current"
                    : "inventory.suggest.all_good";

                return new TelegramRouteResponse(
                    "inventory.suggest.empty",
                    EnsureInfoMarker(_navigationPresenter.GetText(messageKey, locale, withoutCurrentQuantity)),
                    InlineKeyboard(_navigationPresenter.BuildInventoryKeyboard(locale)));
            }

            var sb = new StringBuilder();
            sb.AppendLine(EnsureWarningMarker(_navigationPresenter.GetText("inventory.low_stock.title", locale, lowStock.Count)));
            sb.AppendLine();
            foreach (var item in lowStock)
            {
                var cur = item.CurrentQuantity.HasValue ? $"{item.CurrentQuantity.Value:G}" : "?";
                sb.AppendLine(_navigationPresenter.GetText("inventory.low_stock.item", locale, BuildInventoryItemTitle(item), cur));
            }
            return new TelegramRouteResponse(
                "inventory.suggest",
                sb.ToString().TrimEnd(),
                InlineKeyboard(_navigationPresenter.BuildInventoryKeyboard(locale)));
        }

        // ── Photo: store resolution callbacks ─────────────────────────────────
        if (string.Equals(callbackData, CallbackDataConstants.Inventory.PhotoStoreAdd, StringComparison.Ordinal))
        {
            var pendingKey = BuildPendingChatActionKey(scope);
            if (_pendingStateStore.InventoryPhotoSessions.TryGetValue(pendingKey, out var session)
                && !string.IsNullOrWhiteSpace(session.DetectedStoreNameEn))
            {
                _pendingStateStore.InventoryPhotoSessions[pendingKey] = session with { ResolvedStoreName = session.DetectedStoreNameEn };
                await _foodTrackingService.SaveStoreAliasAsync(
                    session.DetectedStoreNameEn,
                    session.DetectedStoreNameEn,
                    cancellationToken);
                return await TransitionToUnknownItemsOrFinishAsync(scope, locale, pendingKey, cancellationToken);
            }
            return ExpiredPhotoSession(locale);
        }

        if (string.Equals(callbackData, CallbackDataConstants.Inventory.PhotoStoreSkip, StringComparison.Ordinal))
        {
            var pendingKey = BuildPendingChatActionKey(scope);
            if (_pendingStateStore.InventoryPhotoSessions.ContainsKey(pendingKey))
            {
                return await TransitionToUnknownItemsOrFinishAsync(scope, locale, pendingKey, cancellationToken);
            }
            return ExpiredPhotoSession(locale);
        }

        if (string.Equals(callbackData, CallbackDataConstants.Inventory.PhotoStorePickExisting, StringComparison.Ordinal))
        {
            var pendingKey = BuildPendingChatActionKey(scope);
            if (_pendingStateStore.InventoryPhotoSessions.ContainsKey(pendingKey))
            {
                var stores = await _foodTrackingService.GetDistinctStoresAsync(cancellationToken);
                if (stores.Count == 0)
                {
                    return await TransitionToUnknownItemsOrFinishAsync(scope, locale, pendingKey, cancellationToken);
                }
                return new TelegramRouteResponse(
                    "inventory.photo.store.pick",
                    EnsureQuestionMarker(_navigationPresenter.GetText("inventory.photo.store.pick_prompt", locale)),
                    InlineKeyboard(_navigationPresenter.BuildPhotoStorePickExistingKeyboard(locale, stores)));
            }
            return ExpiredPhotoSession(locale);
        }

        if (callbackData.StartsWith(CallbackDataConstants.Inventory.PhotoStoreSelectPrefix, StringComparison.Ordinal))
        {
            var pendingKey = BuildPendingChatActionKey(scope);
            if (_pendingStateStore.InventoryPhotoSessions.TryGetValue(pendingKey, out var session))
            {
                var selectedStore = callbackData[CallbackDataConstants.Inventory.PhotoStoreSelectPrefix.Length..];
                _pendingStateStore.InventoryPhotoSessions[pendingKey] = session with { ResolvedStoreName = selectedStore };
                if (!string.IsNullOrWhiteSpace(session.DetectedStoreNameEn) && !string.IsNullOrWhiteSpace(selectedStore))
                {
                    await _foodTrackingService.SaveStoreAliasAsync(
                        session.DetectedStoreNameEn,
                        selectedStore,
                        cancellationToken);
                }
                return await TransitionToUnknownItemsOrFinishAsync(scope, locale, pendingKey, cancellationToken);
            }
            return ExpiredPhotoSession(locale);
        }

        // ── Photo: unknown items callbacks ─────────────────────────────────
        if (string.Equals(callbackData, CallbackDataConstants.Inventory.PhotoUnknownAddAll, StringComparison.Ordinal))
        {
            var pendingKey = BuildPendingChatActionKey(scope);
            if (_pendingStateStore.InventoryPhotoSessions.TryGetValue(pendingKey, out var session))
            {
                return await AddUnknownItemsToInventoryAsync(locale, pendingKey, session, session.Unknown, cancellationToken);
            }
            return ExpiredPhotoSession(locale);
        }

        if (string.Equals(callbackData, CallbackDataConstants.Inventory.PhotoUnknownSelect, StringComparison.Ordinal))
        {
            var pendingKey = BuildPendingChatActionKey(scope);
            if (_pendingStateStore.InventoryPhotoSessions.ContainsKey(pendingKey))
            {
                _pendingStateStore.ChatActions[pendingKey] = PendingChatActionKind.InventoryPhotoAwaitingUnknownSelection;
                return new TelegramRouteResponse(
                    "inventory.photo.unknown.select_prompt",
                    EnsureQuestionMarker(_navigationPresenter.GetText("inventory.photo.unknown.select_prompt", locale)),
                    InlineKeyboard(_navigationPresenter.BuildPhotoUnknownItemsKeyboard(locale)));
            }
            return ExpiredPhotoSession(locale);
        }

        if (string.Equals(callbackData, CallbackDataConstants.Inventory.PhotoUnknownLink, StringComparison.Ordinal))
        {
            var pendingKey = BuildPendingChatActionKey(scope);
            if (_pendingStateStore.InventoryPhotoSessions.TryGetValue(pendingKey, out var session)
                && session.Unknown.Count > 0)
            {
                _pendingStateStore.ChatActions[pendingKey] = PendingChatActionKind.InventoryPhotoAwaitingItemLink;
                return new TelegramRouteResponse(
                    "inventory.photo.unknown.link_prompt",
                    EnsureQuestionMarker(_navigationPresenter.GetText("inventory.photo.unknown.link_prompt", locale)),
                    InlineKeyboard(_navigationPresenter.BuildPhotoUnknownItemsKeyboard(locale)));
            }

            return ExpiredPhotoSession(locale);
        }

        if (string.Equals(callbackData, CallbackDataConstants.Inventory.PhotoUnknownSkip, StringComparison.Ordinal))
        {
            var pendingKey = BuildPendingChatActionKey(scope);
            _pendingStateStore.ChatActions.TryRemove(pendingKey, out _);
            _pendingStateStore.InventoryPhotoSessions.TryRemove(pendingKey, out _);
            return new TelegramRouteResponse(
                "inventory.photo.done",
                EnsureInfoMarker(_navigationPresenter.GetText("inventory.photo.done", locale)),
                InlineKeyboard(_navigationPresenter.BuildInventoryKeyboard(locale)));
        }

        if (callbackData.StartsWith(CallbackDataConstants.Inventory.CartPrefix, StringComparison.Ordinal))
        {
            var idStr = callbackData[CallbackDataConstants.Inventory.CartPrefix.Length..];
            if (int.TryParse(idStr, out var foodItemId))
            {
                try
                {
                    var added = await _foodTrackingService.AddToShoppingFromInventoryAsync(foodItemId, quantity: null, store: null, cancellationToken);
                    return new TelegramRouteResponse(
                        "inventory.cart.added",
                        _navigationPresenter.GetText("inventory.cart.added", locale, added.Name),
                        InlineKeyboard(_navigationPresenter.BuildInventoryKeyboard(locale)));
                }
                catch (InvalidOperationException)
                {
                    return new TelegramRouteResponse(
                        "inventory.cart.not_found",
                        _navigationPresenter.GetText("inventory.cart.not_found", locale),
                        InlineKeyboard(_navigationPresenter.BuildInventoryKeyboard(locale)));
                }
            }
        }

        return new TelegramRouteResponse(
            "inventory.unknown",
            _navigationPresenter.GetText("stub.wip", locale),
            InlineKeyboard(_navigationPresenter.BuildInventoryKeyboard(locale)));
    }

    private async Task<TelegramRouteResponse> HandleInventoryTextAsync(
        TelegramInboundMessage inbound,
        string locale,
        ConversationScope scope,
        CancellationToken cancellationToken)
    {
        var text = inbound.Text?.Trim() ?? string.Empty;
        var hasPhoto = !string.IsNullOrWhiteSpace(inbound.PhotoFileId);

        if (_foodTrackingService is null || (!hasPhoto && string.IsNullOrWhiteSpace(text)))
        {
            return new TelegramRouteResponse(
                "inventory.text",
                _navigationPresenter.GetText("stub.wip", locale),
                InlineKeyboard(_navigationPresenter.BuildInventoryKeyboard(locale)));
        }

        var pendingKey = BuildPendingChatActionKey(scope);

        if (_pendingStateStore.ChatActions.TryGetValue(pendingKey, out var photoPendingAction)
            && photoPendingAction == PendingChatActionKind.InventoryPhotoAwaitingImage)
        {
            _pendingStateStore.InventoryPhotoSessions.TryGetValue(pendingKey, out var pendingSession);

            if (!hasPhoto)
            {
                var promptKey = pendingSession?.Mode == TelegramInventoryPhotoMode.Consumption
                    ? "inventory.photo.awaiting_consume"
                    : "inventory.photo.awaiting_restock";

                return new TelegramRouteResponse(
                    "inventory.photo.awaiting_image",
                    EnsureQuestionMarker(_navigationPresenter.GetText(promptKey, locale)),
                    InlineKeyboard(_navigationPresenter.BuildInventoryKeyboard(locale)));
            }

            if (pendingSession is null)
            {
                _pendingStateStore.ChatActions.TryRemove(pendingKey, out _);
                return new TelegramRouteResponse(
                    "inventory.photo.expired",
                    EnsureWarningMarker(_navigationPresenter.GetText("inventory.photo.expired", locale)),
                    InlineKeyboard(_navigationPresenter.BuildInventoryKeyboard(locale)));
            }

            var allInventory = await _foodTrackingService.GetAllInventoryAsync(0, cancellationToken);
            var inventoryById = allInventory.ToDictionary(item => item.Id);
            var hints = allInventory
                .Select(item => new TelegramInventoryItemHint(item.Id, item.Name))
                .ToList();

            var analysis = await _importSourceReader.AnalyzeInventoryPhotoAsync(
                inbound.PhotoFileId!,
                pendingSession.Mode,
                hints,
                cancellationToken);

            if (!analysis.Success)
            {
                var errorMessage = analysis.Error;
                if (!string.IsNullOrWhiteSpace(errorMessage)
                    && errorMessage.StartsWith("inventory.", StringComparison.Ordinal))
                {
                    errorMessage = _navigationPresenter.GetText(errorMessage, locale);
                }

                return new TelegramRouteResponse(
                    "inventory.photo.failed",
                    EnsureWarningMarker(errorMessage ?? _navigationPresenter.GetText("inventory.photo.failed", locale)),
                    InlineKeyboard(_navigationPresenter.BuildInventoryKeyboard(locale)));
            }

            var candidates = analysis.Candidates
                .OrderByDescending(candidate => candidate.Confidence)
                .ThenBy(candidate => candidate.Name, StringComparer.OrdinalIgnoreCase)
                .Select((candidate, index) =>
                {
                    inventoryById.TryGetValue(candidate.ItemId, out var matchedInventoryItem);
                    return new PendingInventoryPhotoCandidate(
                        Number: index + 1,
                        candidate.ItemId,
                        candidate.Name,
                        candidate.Quantity,
                        candidate.Unit,
                        candidate.Confidence,
                        matchedInventoryItem?.IconEmoji,
                        matchedInventoryItem?.Category,
                        candidate.PriceTotal,
                        candidate.PricePerUnit);
                })
                .ToList();

            var unknown = analysis.Unknown
                .Where(entry => entry.NameEn is not null || entry.Name is not null)
                .OrderByDescending(entry => entry.Confidence)
                .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .Select((entry, index) => new PendingInventoryPhotoUnknown(
                    Number: index + 1,
                    entry.Name,
                    entry.NameEn,
                    entry.Quantity,
                    entry.Unit,
                    entry.Confidence,
                    entry.PriceTotal,
                    entry.PricePerUnit,
                    entry.IsNonProduct,
                    Category: DefaultUnknownInventoryCategory))
                .ToList();

            var aliasPromotedCandidates = new List<PendingInventoryPhotoCandidate>();
            var unresolvedUnknown = new List<PendingInventoryPhotoUnknown>();
            foreach (var unknownEntry in unknown)
            {
                var aliasItemId = string.IsNullOrWhiteSpace(unknownEntry.NameEn)
                    ? null
                    : await _foodTrackingService.ResolveItemAliasAsync(unknownEntry.NameEn, cancellationToken);

                if (aliasItemId.HasValue && inventoryById.TryGetValue(aliasItemId.Value, out var aliasedItem))
                {
                    aliasPromotedCandidates.Add(new PendingInventoryPhotoCandidate(
                        Number: 0,
                        ItemId: aliasedItem.Id,
                        Name: aliasedItem.Name,
                        Quantity: unknownEntry.Quantity,
                        Unit: unknownEntry.Unit,
                        Confidence: unknownEntry.Confidence,
                        IconEmoji: aliasedItem.IconEmoji,
                        Category: aliasedItem.Category,
                        PriceTotal: unknownEntry.PriceTotal,
                        PricePerUnit: unknownEntry.PricePerUnit));
                    continue;
                }

                unresolvedUnknown.Add(unknownEntry);
            }

            candidates = candidates
                .Concat(aliasPromotedCandidates)
                .OrderByDescending(candidate => candidate.Confidence)
                .ThenBy(candidate => candidate.Name, StringComparer.OrdinalIgnoreCase)
                .Select((candidate, index) => candidate with { Number = index + 1 })
                .ToList();

            unknown = unresolvedUnknown
                .OrderByDescending(entry => entry.Confidence)
                .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .Select((entry, index) => entry with { Number = index + 1 })
                .ToList();

            if (candidates.Count == 0 && unknown.Count == 0)
            {
                _pendingStateStore.ChatActions.TryRemove(pendingKey, out _);
                _pendingStateStore.InventoryPhotoSessions.TryRemove(pendingKey, out _);
                return new TelegramRouteResponse(
                    "inventory.photo.empty",
                    EnsureInfoMarker(_navigationPresenter.GetText("inventory.photo.empty", locale)),
                    InlineKeyboard(_navigationPresenter.BuildInventoryKeyboard(locale)));
            }

            var updatedSession = pendingSession with
            {
                Candidates = candidates,
                Unknown = unknown,
                NonProducts = analysis.NonProducts,
                DetectedStoreName = analysis.DetectedStore?.Name,
                DetectedStoreNameEn = analysis.DetectedStore?.NameEn,
                StoreConfidence = analysis.DetectedStore?.Confidence
            };
            _pendingStateStore.InventoryPhotoSessions[pendingKey] = updatedSession;
            _pendingStateStore.ChatActions[pendingKey] = PendingChatActionKind.InventoryPhotoAwaitingSelection;

            return new TelegramRouteResponse(
                "inventory.photo.preview",
                BuildInventoryPhotoPreviewText(locale, pendingSession.Mode, candidates, unknown, updatedSession),
                InlineKeyboard(_navigationPresenter.BuildInventoryPhotoConfirmKeyboard(locale)));
        }

        if (_pendingStateStore.ChatActions.TryGetValue(pendingKey, out var selectionPendingAction)
            && selectionPendingAction == PendingChatActionKind.InventoryPhotoAwaitingSelection)
        {
            if (!_pendingStateStore.InventoryPhotoSessions.TryGetValue(pendingKey, out var session))
            {
                _pendingStateStore.ChatActions.TryRemove(pendingKey, out _);
                return new TelegramRouteResponse(
                    "inventory.photo.expired",
                    EnsureWarningMarker(_navigationPresenter.GetText("inventory.photo.expired", locale)),
                    InlineKeyboard(_navigationPresenter.BuildInventoryKeyboard(locale)));
            }

            if (UrlCancelTokens.Contains(text))
            {
                _pendingStateStore.ChatActions.TryRemove(pendingKey, out _);
                _pendingStateStore.InventoryPhotoSessions.TryRemove(pendingKey, out _);
                return new TelegramRouteResponse(
                    "inventory.photo.cancelled",
                    EnsureInfoMarker(_navigationPresenter.GetText("inventory.photo.cancelled", locale)),
                    InlineKeyboard(_navigationPresenter.BuildInventoryKeyboard(locale)));
            }

            IReadOnlyList<int> selectedNumbers;
            if (UrlSelectAllTokens.Contains(text))
            {
                selectedNumbers = session.Candidates.Select(x => x.Number).ToList();
            }
            else
            {
                selectedNumbers = SelectionNumberRegex
                    .Matches(text)
                    .Select(match => match.Value)
                    .Distinct(StringComparer.Ordinal)
                    .Select(value => int.TryParse(value, out var parsed) ? parsed : 0)
                    .Where(number => number > 0)
                    .ToList();
            }

            if (selectedNumbers.Count == 0)
            {
                return new TelegramRouteResponse(
                    "inventory.photo.invalid_selection",
                    EnsureWarningMarker(_navigationPresenter.GetText("inventory.photo.invalid_selection", locale)),
                    InlineKeyboard(_navigationPresenter.BuildInventoryPhotoConfirmKeyboard(locale)));
            }

            var selectedCandidates = session.Candidates
                .Where(candidate => selectedNumbers.Contains(candidate.Number))
                .OrderBy(candidate => candidate.Number)
                .ToList();

            if (selectedCandidates.Count == 0)
            {
                return new TelegramRouteResponse(
                    "inventory.photo.invalid_selection",
                    EnsureWarningMarker(_navigationPresenter.GetText("inventory.photo.invalid_selection", locale)),
                    InlineKeyboard(_navigationPresenter.BuildInventoryPhotoConfirmKeyboard(locale)));
            }

            return await ApplyInventoryPhotoCandidatesAsync(scope, locale, pendingKey, session, selectedCandidates, cancellationToken);
        }

        if (_pendingStateStore.ChatActions.TryGetValue(pendingKey, out var unknownPendingAction)
            && unknownPendingAction == PendingChatActionKind.InventoryPhotoAwaitingUnknownSelection)
        {
            if (!_pendingStateStore.InventoryPhotoSessions.TryGetValue(pendingKey, out var unknownSession))
            {
                _pendingStateStore.ChatActions.TryRemove(pendingKey, out _);
                return ExpiredPhotoSession(locale);
            }

            IReadOnlyList<int> selectedUnknownNumbers;
            if (UrlSelectAllTokens.Contains(text))
            {
                selectedUnknownNumbers = unknownSession.Unknown.Select(x => x.Number).ToList();
            }
            else
            {
                selectedUnknownNumbers = SelectionNumberRegex
                    .Matches(text)
                    .Select(match => match.Value)
                    .Distinct(StringComparer.Ordinal)
                    .Select(value => int.TryParse(value, out var parsed) ? parsed : 0)
                    .Where(number => number > 0)
                    .ToList();
            }

            if (selectedUnknownNumbers.Count == 0)
            {
                return new TelegramRouteResponse(
                    "inventory.photo.invalid_selection",
                    EnsureWarningMarker(_navigationPresenter.GetText("inventory.photo.invalid_selection", locale)),
                    InlineKeyboard(_navigationPresenter.BuildPhotoUnknownItemsKeyboard(locale)));
            }

            var selectedUnknowns = unknownSession.Unknown
                .Where(u => selectedUnknownNumbers.Contains(u.Number))
                .ToList();

            if (selectedUnknowns.Count == 0)
            {
                return new TelegramRouteResponse(
                    "inventory.photo.invalid_selection",
                    EnsureWarningMarker(_navigationPresenter.GetText("inventory.photo.invalid_selection", locale)),
                    InlineKeyboard(_navigationPresenter.BuildPhotoUnknownItemsKeyboard(locale)));
            }

            return await AddUnknownItemsToInventoryAsync(locale, pendingKey, unknownSession, selectedUnknowns, cancellationToken);
        }

        if (_pendingStateStore.ChatActions.TryGetValue(pendingKey, out var itemLinkPendingAction)
            && itemLinkPendingAction == PendingChatActionKind.InventoryPhotoAwaitingItemLink)
        {
            if (!_pendingStateStore.InventoryPhotoSessions.TryGetValue(pendingKey, out var linkSession))
            {
                _pendingStateStore.ChatActions.TryRemove(pendingKey, out _);
                return ExpiredPhotoSession(locale);
            }

            if (!TryParseInventoryUnknownItemLinks(text, out var links))
            {
                return new TelegramRouteResponse(
                    "inventory.photo.unknown.link_invalid",
                    EnsureWarningMarker(_navigationPresenter.GetText("inventory.photo.unknown.link_invalid", locale)),
                    InlineKeyboard(_navigationPresenter.BuildPhotoUnknownItemsKeyboard(locale)));
            }

            var unknownByNumber = linkSession.Unknown.ToDictionary(x => x.Number);
            var availableInventoryItems = await _foodTrackingService.GetAllInventoryAsync(0, cancellationToken);
            var inventoryById = availableInventoryItems.ToDictionary(x => x.Id);

            var appliedLines = new List<string>();
            var linkedCount = 0;
            var missingIds = new HashSet<int>();
            var resolvedUnknownNumbers = new HashSet<int>();

            foreach (var (unknownNumber, targetItemId) in links)
            {
                if (!unknownByNumber.TryGetValue(unknownNumber, out var unknownEntry))
                {
                    continue;
                }

                if (!inventoryById.TryGetValue(targetItemId, out var targetItem))
                {
                    missingIds.Add(targetItemId);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(unknownEntry.NameEn))
                {
                    continue;
                }

                await _foodTrackingService.SaveItemAliasAsync(unknownEntry.NameEn, targetItemId, cancellationToken);

                var signedDelta = linkSession.Mode == TelegramInventoryPhotoMode.Consumption
                    ? -unknownEntry.Quantity
                    : unknownEntry.Quantity;

                var updated = await _foodTrackingService.AdjustInventoryQuantityAsync(targetItemId, signedDelta, cancellationToken);
                if (linkSession.Mode == TelegramInventoryPhotoMode.Restock && unknownEntry.PricePerUnit.HasValue)
                {
                    await _foodTrackingService.UpdateInventoryPriceAndStoreAsync(
                        targetItemId,
                        unknownEntry.PricePerUnit,
                        linkSession.ResolvedStoreName,
                        cancellationToken);
                }

                resolvedUnknownNumbers.Add(unknownNumber);
                linkedCount++;
                appliedLines.Add(_navigationPresenter.GetText(
                    "inventory.photo.applied.item",
                    locale,
                    BuildInventoryItemTitle(targetItem),
                    updated.CurrentQuantity?.ToString("0.##", CultureInfo.InvariantCulture) ?? "0",
                    signedDelta > 0 ? "+" : "-"));
            }

            if (linkedCount == 0)
            {
                return new TelegramRouteResponse(
                    "inventory.photo.unknown.link_invalid",
                    EnsureWarningMarker(_navigationPresenter.GetText("inventory.photo.unknown.link_invalid", locale)),
                    InlineKeyboard(_navigationPresenter.BuildPhotoUnknownItemsKeyboard(locale)));
            }

            var remainingUnknown = linkSession.Unknown
                .Where(x => !resolvedUnknownNumbers.Contains(x.Number))
                .OrderByDescending(x => x.Confidence)
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Select((entry, index) => entry with { Number = index + 1 })
                .ToList();

            var updatedSession = linkSession with { Unknown = remainingUnknown };
            _pendingStateStore.InventoryPhotoSessions[pendingKey] = updatedSession;

            var summary = new StringBuilder();
            summary.AppendLine(EnsureInfoMarker(_navigationPresenter.GetText("inventory.photo.unknown.link_done", locale, linkedCount)));
            foreach (var line in appliedLines)
            {
                summary.AppendLine(line);
            }

            foreach (var missingId in missingIds.OrderBy(x => x))
            {
                summary.AppendLine(EnsureWarningMarker(_navigationPresenter.GetText("inventory.photo.unknown.link_not_found", locale, missingId)));
            }

            if (remainingUnknown.Count > 0)
            {
                summary.AppendLine();
                summary.AppendLine($"📦 {_navigationPresenter.GetText("inventory.photo.unknown.offer_title", locale, remainingUnknown.Count)}");
                foreach (var entry in remainingUnknown)
                {
                    var displayName = BuildUnknownDisplayName(entry);
                    var entryQuantity = entry.Quantity.ToString("0.##", CultureInfo.InvariantCulture);
                    var unit = string.IsNullOrWhiteSpace(entry.Unit) ? string.Empty : $" {entry.Unit}";
                    summary.AppendLine($"{entry.Number}) {displayName} — {entryQuantity}{unit}");
                }

                summary.AppendLine();
                summary.AppendLine(EnsureQuestionMarker(_navigationPresenter.GetText("inventory.photo.unknown.offer_prompt", locale)));
                _pendingStateStore.ChatActions[pendingKey] = PendingChatActionKind.InventoryPhotoAwaitingUnknownSelection;

                return new TelegramRouteResponse(
                    "inventory.photo.unknown.offer",
                    summary.ToString().TrimEnd(),
                    InlineKeyboard(_navigationPresenter.BuildPhotoUnknownItemsKeyboard(locale)));
            }

            _pendingStateStore.ChatActions.TryRemove(pendingKey, out _);
            _pendingStateStore.InventoryPhotoSessions.TryRemove(pendingKey, out _);
            return new TelegramRouteResponse(
                "inventory.photo.unknown.link_done",
                summary.ToString().TrimEnd(),
                InlineKeyboard(_navigationPresenter.BuildInventoryKeyboard(locale)));
        }

        if (hasPhoto)
        {
            return new TelegramRouteResponse(
                "inventory.photo.mode_required",
                EnsureInfoMarker(_navigationPresenter.GetText("inventory.photo.mode_required", locale)),
                InlineKeyboard(_navigationPresenter.BuildInventoryKeyboard(locale)));
        }

        if (_pendingStateStore.ChatActions.TryGetValue(pendingKey, out var pendingAction)
            && pendingAction == PendingChatActionKind.InventorySearch)
        {
            _pendingStateStore.ChatActions.TryRemove(pendingKey, out _);
            var results = await _foodTrackingService.SearchInventoryAsync(text, take: 10, cancellationToken);

            if (results.Count == 0)
            {
                return new TelegramRouteResponse(
                    "inventory.search.empty",
                    _navigationPresenter.GetText("inventory.empty", locale),
                    InlineKeyboard(_navigationPresenter.BuildInventoryKeyboard(locale)));
            }

            var sb = new StringBuilder();
            foreach (var item in results)
            {
                sb.AppendLine($"  [{item.Id}] {BuildInventoryItemTitle(item)}{BuildInventoryQuantitySuffix(item)}");
            }
            sb.AppendLine();
            sb.AppendLine(EnsureInfoMarker(_navigationPresenter.GetText("inventory.add_to_cart_hint", locale)));

            return new TelegramRouteResponse(
                "inventory.search.results",
                sb.ToString().TrimEnd(),
                InlineKeyboard(_navigationPresenter.BuildInventoryKeyboard(locale)));
        }

        if (_pendingStateStore.ChatActions.TryGetValue(pendingKey, out pendingAction)
            && pendingAction == PendingChatActionKind.InventoryAdjustQuantity)
        {
            if (!TryParseInventoryQuantityUpdates(text, out var updates))
            {
                return new TelegramRouteResponse(
                    "inventory.adjust.invalid",
                    EnsureWarningMarker(_navigationPresenter.GetText("inventory.adjust.invalid", locale)),
                    InlineKeyboard(_navigationPresenter.BuildInventoryKeyboard(locale)));
            }

            var successLines = new List<string>();
            var notFoundIds = new List<int>();
            foreach (var update in updates)
            {
                try
                {
                    var updated = update.Mode == InventoryQuantityUpdateMode.Delta
                        ? await _foodTrackingService.AdjustInventoryQuantityAsync(
                            update.ItemId,
                            update.Value,
                            cancellationToken)
                        : await _foodTrackingService.SetInventoryCurrentQuantityAsync(
                            update.ItemId,
                            update.Value,
                            cancellationToken);

                    var quantityText = updated.CurrentQuantity?.ToString("0.##", CultureInfo.InvariantCulture) ?? "0";
                    var successText = StripLeadingStatusMarkers(
                        _navigationPresenter.GetText("inventory.adjust.done", locale, updated.Name, quantityText),
                        out _);
                    successLines.Add($"{BuildInventoryItemTitle(updated)} {successText}");
                }
                catch (InvalidOperationException)
                {
                    notFoundIds.Add(update.ItemId);
                }
            }

            if (successLines.Count == 0)
            {
                return new TelegramRouteResponse(
                    "inventory.adjust.not_found",
                    EnsureWarningMarker(_navigationPresenter.GetText("inventory.adjust.not_found", locale, notFoundIds.FirstOrDefault())),
                    InlineKeyboard(_navigationPresenter.BuildInventoryKeyboard(locale)));
            }

            _pendingStateStore.ChatActions.TryRemove(pendingKey, out _);

            var responseLines = new List<string>(successLines);
            responseLines.AddRange(notFoundIds
                .Distinct()
                .Select(id => EnsureWarningMarker(_navigationPresenter.GetText("inventory.adjust.not_found", locale, id))));

            return new TelegramRouteResponse(
                "inventory.adjust.done",
                string.Join(Environment.NewLine, responseLines),
                InlineKeyboard(_navigationPresenter.BuildInventoryKeyboard(locale)));
        }

        if (_pendingStateStore.ChatActions.TryGetValue(pendingKey, out pendingAction)
            && pendingAction == PendingChatActionKind.InventorySetMinQuantity)
        {
            if (!TryParseInventoryMinQuantityUpdates(text, out var updates))
            {
                return new TelegramRouteResponse(
                    "inventory.min.invalid",
                    EnsureWarningMarker(_navigationPresenter.GetText("inventory.min.invalid", locale)),
                    InlineKeyboard(_navigationPresenter.BuildInventoryKeyboard(locale)));
            }

            var currentMinById = (await _foodTrackingService.GetAllInventoryAsync(0, cancellationToken))
                .ToDictionary(item => item.Id, item => item.MinQuantity ?? 0m);
            var successLines = new List<string>();
            var notFoundIds = new List<int>();
            foreach (var update in updates)
            {
                try
                {
                    var targetMin = update.Mode == InventoryQuantityUpdateMode.Delta
                        ? Math.Max(0m, (currentMinById.TryGetValue(update.ItemId, out var current) ? current : 0m) + update.Value)
                        : update.Value;

                    var updated = await _foodTrackingService.SetInventoryMinQuantityAsync(
                        update.ItemId,
                        targetMin,
                        cancellationToken);

                    currentMinById[updated.Id] = updated.MinQuantity ?? targetMin;
                    var minQuantityText = updated.MinQuantity?.ToString("0.##", CultureInfo.InvariantCulture) ?? "0";
                    var successText = StripLeadingStatusMarkers(
                        _navigationPresenter.GetText("inventory.min.done", locale, updated.Name, minQuantityText),
                        out _);
                    successLines.Add($"{BuildInventoryItemTitle(updated)} {successText}");
                }
                catch (InvalidOperationException)
                {
                    notFoundIds.Add(update.ItemId);
                }
            }

            if (successLines.Count == 0)
            {
                return new TelegramRouteResponse(
                    "inventory.min.not_found",
                    EnsureWarningMarker(_navigationPresenter.GetText("inventory.min.not_found", locale, notFoundIds.FirstOrDefault())),
                    InlineKeyboard(_navigationPresenter.BuildInventoryKeyboard(locale)));
            }

            _pendingStateStore.ChatActions.TryRemove(pendingKey, out _);

            var responseLines = new List<string>(successLines);
            responseLines.AddRange(notFoundIds
                .Distinct()
                .Select(id => EnsureWarningMarker(_navigationPresenter.GetText("inventory.min.not_found", locale, id))));

            return new TelegramRouteResponse(
                "inventory.min.done",
                string.Join(Environment.NewLine, responseLines),
                InlineKeyboard(_navigationPresenter.BuildInventoryKeyboard(locale)));
        }

        if (TryParseInventoryCartSelection(text, out var itemId, out var quantity))
        {
            try
            {
                var added = await _foodTrackingService.AddToShoppingFromInventoryAsync(itemId, quantity, store: null, cancellationToken);
                return new TelegramRouteResponse(
                    "inventory.cart.added",
                    _navigationPresenter.GetText("inventory.cart.added", locale, added.Name),
                    InlineKeyboard(_navigationPresenter.BuildInventoryKeyboard(locale)));
            }
            catch (InvalidOperationException)
            {
                return new TelegramRouteResponse(
                    "inventory.cart.not_found",
                    _navigationPresenter.GetText("inventory.cart.not_found", locale),
                    InlineKeyboard(_navigationPresenter.BuildInventoryKeyboard(locale)));
            }
        }

        var searchResults = await _foodTrackingService.SearchInventoryAsync(text, take: 10, cancellationToken);
        if (searchResults.Count == 0)
        {
            return new TelegramRouteResponse(
                "inventory.search.empty",
                _navigationPresenter.GetText("inventory.empty", locale),
                InlineKeyboard(_navigationPresenter.BuildInventoryKeyboard(locale)));
        }

        var resultSb = new StringBuilder();
        foreach (var item in searchResults)
        {
            resultSb.AppendLine($"  [{item.Id}] {BuildInventoryItemTitle(item)}{BuildInventoryQuantitySuffix(item)}");
        }
        resultSb.AppendLine();
        resultSb.AppendLine(EnsureInfoMarker(_navigationPresenter.GetText("inventory.add_to_cart_hint", locale)));

        return new TelegramRouteResponse(
            "inventory.search.results",
            resultSb.ToString().TrimEnd(),
            InlineKeyboard(_navigationPresenter.BuildInventoryKeyboard(locale)));
    }

    private async Task<TelegramRouteResponse> HandleFoodCallbackAsync(
        string callbackData,
        string locale,
        ConversationScope scope,
        CancellationToken cancellationToken)
    {
        if (string.Equals(callbackData, CallbackDataConstants.Weekly.Analytics, StringComparison.Ordinal))
        {
            return new TelegramRouteResponse(
                "nav.weekly.analytics",
                _navigationPresenter.GetText("menu.weekly.analytics", locale),
                InlineKeyboard(_navigationPresenter.BuildWeeklyAnalyticsKeyboard(locale)));
        }

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

        if (string.Equals(callbackData, CallbackDataConstants.Shop.Delete, StringComparison.Ordinal))
        {
            return await HandleShoppingDeleteStartAsync(locale, scope, cancellationToken);
        }

        if (string.Equals(callbackData, CallbackDataConstants.Shop.Add, StringComparison.Ordinal))
        {
            if (_foodTrackingService is null)
            {
                return new TelegramRouteResponse(
                    "food.shop.add.prompt",
                    EnsureQuestionMarker(_navigationPresenter.GetText("food.shop.add.prompt", locale)),
                    InlineKeyboard(_navigationPresenter.BuildShoppingKeyboard(locale)));
            }

            var items = await _foodTrackingService.GetAllInventoryAsync(0, cancellationToken);
            if (items.Count == 0)
            {
                return new TelegramRouteResponse(
                    "inventory.list.empty",
                    _navigationPresenter.GetText("inventory.empty", locale),
                    InlineKeyboard(_navigationPresenter.BuildShoppingKeyboard(locale)));
            }

            return new TelegramRouteResponse(
                "food.shop.add.from_inventory",
                BuildInventoryCatalogText(items, locale),
                InlineKeyboard(_navigationPresenter.BuildShoppingKeyboard(locale)));
        }

        if (callbackData.StartsWith(CallbackDataConstants.Shop.Prefix, StringComparison.Ordinal))
        {
            _pendingStateStore.ShoppingDeleteSessions.TryRemove(BuildPendingShoppingDeleteKey(scope), out _);
        }

        var result = await _orchestrator.ProcessAsync(
            callbackData,
            TelegramChannel,
            locale,
            scope.UserId,
            scope.ConversationId,
            cancellationToken);

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
        ConversationScope scope,
        CancellationToken cancellationToken)
    {
        if (_foodTrackingService is null || string.IsNullOrWhiteSpace(text))
        {
            return new TelegramRouteResponse(
                "shopping.text",
                _navigationPresenter.GetText("stub.wip", locale),
                InlineKeyboard(_navigationPresenter.BuildShoppingKeyboard(locale)));
        }

        var pendingDeleteKey = BuildPendingShoppingDeleteKey(scope);
        if (_pendingStateStore.ShoppingDeleteSessions.TryGetValue(pendingDeleteKey, out var pendingDeleteSession))
        {
            return await HandleShoppingDeleteSelectionAsync(
                text,
                locale,
                pendingDeleteKey,
                pendingDeleteSession,
                cancellationToken);
        }

        if (TryParseInventoryCartSelection(text, out var inventoryItemId, out var directQuantity))
        {
            try
            {
                var addedById = await _foodTrackingService.AddToShoppingFromInventoryAsync(
                    inventoryItemId,
                    directQuantity,
                    store: null,
                    cancellationToken);

                var qtySuffix = BuildShoppingQuantitySuffix(addedById.Quantity, locale);
                var storeSuffix = BuildShoppingStoreSuffix(addedById.Store, locale);

                return new TelegramRouteResponse(
                    "food.shop.added",
                    _navigationPresenter.GetText("food.shop.added", locale, addedById.Name, qtySuffix, storeSuffix),
                    InlineKeyboard(_navigationPresenter.BuildShoppingKeyboard(locale)));
            }
            catch (InvalidOperationException)
            {
                return new TelegramRouteResponse(
                    "inventory.cart.not_found",
                    _navigationPresenter.GetText("inventory.cart.not_found", locale),
                    InlineKeyboard(_navigationPresenter.BuildShoppingKeyboard(locale)));
            }
        }

        var parsed = ShoppingTextInputParser.Parse(text);
        var name = parsed.ProductName;
        var quantity = parsed.Quantity;
        var store = parsed.Store;

        if (string.IsNullOrWhiteSpace(name))
        {
            return new TelegramRouteResponse(
                "food.shop.add.prompt",
                $"{QuestionMarker} {_navigationPresenter.GetText("food.shop.add.prompt", locale)}",
                InlineKeyboard(_navigationPresenter.BuildShoppingKeyboard(locale)));
        }

        if (CyrillicRegex.IsMatch(name))
        {
            return new TelegramRouteResponse(
                "shop.only_english",
                _navigationPresenter.GetText("shop.only_english", locale),
                InlineKeyboard(_navigationPresenter.BuildShoppingKeyboard(locale)));
        }

        // Inventory-first: try to match name against inventory, link if found
        var inventoryMatches = await _foodTrackingService.SearchInventoryAsync(name, take: 1, cancellationToken);
        if (inventoryMatches.Count > 0)
        {
            var match = inventoryMatches[0];
            var added = await _foodTrackingService.AddToShoppingFromInventoryAsync(match.Id, quantity, store, cancellationToken);
            var qty = BuildShoppingQuantitySuffix(added.Quantity, locale);
            var st = BuildShoppingStoreSuffix(added.Store, locale);
            var matchNote = _navigationPresenter.GetText("shop.matched_inventory", locale, match.Name);
            return new TelegramRouteResponse(
                "food.shop.added",
                $"{_navigationPresenter.GetText("food.shop.added", locale, added.Name, qty, st)}\n{matchNote}",
                InlineKeyboard(_navigationPresenter.BuildShoppingKeyboard(locale)));
        }

        // No inventory match — do not add free-text items to shopping.
        var warning = _navigationPresenter.GetText("shop.not_in_inventory", locale, name);
        var englishHint = _navigationPresenter.GetText("shop.add_inventory_first", locale);
        return new TelegramRouteResponse(
            "shop.not_in_inventory",
            $"{warning}\n{englishHint}",
            InlineKeyboard(_navigationPresenter.BuildShoppingKeyboard(locale)));
    }

    private async Task<TelegramRouteResponse> HandleShoppingDeleteStartAsync(
        string locale,
        ConversationScope scope,
        CancellationToken cancellationToken)
    {
        if (_foodTrackingService is null)
        {
            return new TelegramRouteResponse(
                "shopping.text",
                _navigationPresenter.GetText("stub.wip", locale),
                InlineKeyboard(_navigationPresenter.BuildShoppingKeyboard(locale)));
        }

        var items = await _foodTrackingService.GetActiveGroceryListAsync(cancellationToken);
        var inventoryByName = (await _foodTrackingService.GetAllInventoryAsync(0, cancellationToken))
            .GroupBy(x => x.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        if (items.Count == 0)
        {
            _pendingStateStore.ShoppingDeleteSessions.TryRemove(BuildPendingShoppingDeleteKey(scope), out _);
            return new TelegramRouteResponse(
                "food.shop.list.empty",
                _navigationPresenter.GetText("food.shop.list.empty", locale),
                InlineKeyboard(_navigationPresenter.BuildShoppingKeyboard(locale)));
        }

        var candidates = items
            .Select((item, index) => new PendingShoppingDeleteCandidate(
                index + 1,
                item.Id,
                item.Name,
                item.Quantity,
                item.Store))
            .ToList();

        _pendingStateStore.ShoppingDeleteSessions[BuildPendingShoppingDeleteKey(scope)] = new PendingShoppingDeleteSession(candidates);

        var sb = new StringBuilder();
        sb.AppendLine(EnsureQuestionMarker(_navigationPresenter.GetText("food.shop.delete.prompt.title", locale, candidates.Count)));
        sb.AppendLine();

        foreach (var candidate in candidates)
        {
            var qty = string.IsNullOrWhiteSpace(candidate.Quantity) ? string.Empty : $" x {candidate.Quantity}";
            var store = string.IsNullOrWhiteSpace(candidate.Store) ? string.Empty : $" ({candidate.Store})";
            inventoryByName.TryGetValue(candidate.Name.Trim(), out var matchedInventory);
            var displayName = BuildInventoryItemTitle(candidate.Name, matchedInventory?.IconEmoji);
            sb.AppendLine(_navigationPresenter.GetText(
                "food.shop.delete.prompt.item",
                locale,
                candidate.Number,
                $"{displayName}{qty}{store}"));
        }

        sb.AppendLine();
        sb.AppendLine(EnsureInfoMarker(_navigationPresenter.GetText("food.shop.delete.prompt.hint", locale)));

        return new TelegramRouteResponse(
            "food.shop.delete.prompt",
            sb.ToString().TrimEnd(),
            InlineKeyboard(_navigationPresenter.BuildShoppingKeyboard(locale)));
    }

    private async Task<TelegramRouteResponse> HandleShoppingDeleteSelectionAsync(
        string text,
        string locale,
        string pendingDeleteKey,
        PendingShoppingDeleteSession pendingDeleteSession,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new TelegramRouteResponse(
                "food.shop.delete.invalid",
                _navigationPresenter.GetText("food.shop.delete.invalid", locale),
                InlineKeyboard(_navigationPresenter.BuildShoppingKeyboard(locale)));
        }

        var normalized = text.Trim();
        if (normalized.Equals("/cancel", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("cancel", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("скасувати", StringComparison.OrdinalIgnoreCase))
        {
            _pendingStateStore.ShoppingDeleteSessions.TryRemove(pendingDeleteKey, out _);
            return new TelegramRouteResponse(
                "food.shop.delete.cancelled",
                _navigationPresenter.GetText("food.shop.delete.cancelled", locale),
                InlineKeyboard(_navigationPresenter.BuildShoppingKeyboard(locale)));
        }

        var selectedIds = ResolveShoppingDeleteSelection(pendingDeleteSession.Candidates, normalized);
        if (selectedIds.Count == 0)
        {
            return new TelegramRouteResponse(
                "food.shop.delete.no_match",
                _navigationPresenter.GetText("food.shop.delete.no_match", locale),
                InlineKeyboard(_navigationPresenter.BuildShoppingKeyboard(locale)));
        }

        var deleted = await _foodTrackingService!.DeleteItemsByIdsAsync(selectedIds, cancellationToken);
        _pendingStateStore.ShoppingDeleteSessions.TryRemove(pendingDeleteKey, out _);

        return new TelegramRouteResponse(
            "food.shop.delete.done",
            _navigationPresenter.GetText("food.shop.delete.done", locale, deleted),
            InlineKeyboard(_navigationPresenter.BuildShoppingKeyboard(locale)));
    }

    private static IReadOnlyCollection<int> ResolveShoppingDeleteSelection(
        IReadOnlyList<PendingShoppingDeleteCandidate> candidates,
        string input)
    {
        var selected = new HashSet<int>();

        var tokens = input
            .Split([',', ';', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(token => token.Trim())
            .Where(token => token.Length > 0)
            .ToList();

        if (tokens.Count == 0)
        {
            return [];
        }

        foreach (var token in tokens)
        {
            if (TryResolveShoppingDeleteNumber(token, out var number))
            {
                var numericMatch = candidates.FirstOrDefault(candidate => candidate.Number == number);
                if (numericMatch is not null)
                {
                    selected.Add(numericMatch.ItemId);
                }

                var trailingName = ExtractTrailingShoppingDeleteName(token);
                if (string.IsNullOrWhiteSpace(trailingName))
                {
                    continue;
                }

                TryResolveShoppingDeleteByName(candidates, trailingName, selected);
                continue;
            }

            TryResolveShoppingDeleteByName(candidates, token, selected);
        }

        return selected.ToList();
    }

    private static bool TryResolveShoppingDeleteNumber(string token, out int number)
    {
        number = 0;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        if (int.TryParse(token.Trim(), out number))
        {
            return number > 0;
        }

        var match = ShoppingDeleteLeadingNumberRegex.Match(token.Trim());
        return match.Success
               && int.TryParse(match.Groups["number"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out number)
               && number > 0;
    }

    private static string ExtractTrailingShoppingDeleteName(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return string.Empty;
        }

        var match = ShoppingDeleteLeadingNumberRegex.Match(token.Trim());
        return match.Success ? match.Groups["tail"].Value.Trim() : string.Empty;
    }

    private static void TryResolveShoppingDeleteByName(
        IReadOnlyList<PendingShoppingDeleteCandidate> candidates,
        string token,
        ISet<int> selected)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        var normalizedToken = token.Trim();
        var exactMatches = candidates
            .Where(candidate => string.Equals(candidate.Name, normalizedToken, StringComparison.OrdinalIgnoreCase))
            .Select(candidate => candidate.ItemId)
            .ToList();
        if (exactMatches.Count > 0)
        {
            foreach (var id in exactMatches)
            {
                selected.Add(id);
            }

            return;
        }

        var containsMatches = candidates
            .Where(candidate => candidate.Name.Contains(normalizedToken, StringComparison.OrdinalIgnoreCase))
            .Select(candidate => candidate.ItemId)
            .ToList();

        foreach (var id in containsMatches)
        {
            selected.Add(id);
        }
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
                    _navigationPresenter.GetText("food.weekly.portion.not_found", locale, portionMealId),
                    InlineKeyboard(_navigationPresenter.BuildWeeklyMenuKeyboard(locale)));
            }

            var portionSb = new System.Text.StringBuilder();
            portionSb.AppendLine(_navigationPresenter.GetText("food.weekly.portion.title", locale, calc.MealName, calc.TargetServings, calc.Multiplier));
            portionSb.AppendLine();
            foreach (var i in calc.Ingredients)
            {
                var scaled = i.ScaledQuantity ?? i.OriginalQuantity ?? "\u2014";
                portionSb.AppendLine(_navigationPresenter.GetText("food.weekly.portion.ingredient", locale, i.Name, scaled));
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
                    _navigationPresenter.GetText("food.weekly.logged", locale, mealId, servings),
                    InlineKeyboard(_navigationPresenter.BuildWeeklyMenuKeyboard(locale)));
            }
            catch (InvalidOperationException)
            {
                return new TelegramRouteResponse(
                    "food.weekly.log.not_found",
                    _navigationPresenter.GetText("food.weekly.log.not_found", locale, mealId),
                    InlineKeyboard(_navigationPresenter.BuildWeeklyMenuKeyboard(locale)));
            }
        }

        // Otherwise show the meals list.
        var meals = await _foodTrackingService.GetAllMealsAsync(cancellationToken);
        if (meals.Count == 0)
        {
            return new TelegramRouteResponse(
                "food.weekly.view",
                _navigationPresenter.GetText("food.weekly.view.empty", locale),
                InlineKeyboard(_navigationPresenter.BuildWeeklyMenuKeyboard(locale)));
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine(_navigationPresenter.GetText("food.weekly.view.title", locale, meals.Count));
        sb.AppendLine();
        foreach (var meal in meals)
        {
            var calories = BuildCaloriesPerServingSuffix(meal.CaloriesPerServing, locale);
            sb.AppendLine(_navigationPresenter.GetText("food.weekly.view.line", locale, meal.Id, meal.Name, calories));
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
                _navigationPresenter.GetText("food.weekly.create.empty", locale),
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
        preview.AppendLine(_navigationPresenter.GetText("food.weekly.create.preview.title", locale, pending.Name));
        preview.AppendLine(_navigationPresenter.GetText("food.weekly.create.preview.servings", locale, pending.DefaultServings));
        preview.AppendLine();
        preview.AppendLine(_navigationPresenter.GetText("food.weekly.create.preview.confirm", locale));

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
                _navigationPresenter.GetText("food.weekly.create.expired", locale),
                InlineKeyboard(_navigationPresenter.BuildWeeklyMenuKeyboard(locale)));
        }

        if (_foodTrackingService is null)
        {
            return new TelegramRouteResponse(
                "food.weekly.create.error",
                _navigationPresenter.GetText("food.weekly.create.error", locale),
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
            _navigationPresenter.GetText("food.weekly.create.done", locale, meal.Name, meal.Id),
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
            _navigationPresenter.GetText("food.weekly.create.cancelled", locale),
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
                result.Error ?? _navigationPresenter.GetText("food.photo.failed_default", locale),
                InlineKeyboard(_navigationPresenter.BuildWeeklyMenuKeyboard(locale)));
        }

        var pending = new PendingFoodPhotoLog(result.MealName, result.EstimatedCalories.Value, 1m);
        var pendingKey = BuildPendingChatActionKey(scope);
        _pendingStateStore.FoodPhotoLogs[pendingKey] = pending;

        var preview = new System.Text.StringBuilder();
        preview.AppendLine(_navigationPresenter.GetText("food.photo.preview.identified", locale, pending.MealName));
        preview.AppendLine(_navigationPresenter.GetText("food.photo.preview.calories", locale, pending.EstimatedCalories));
        preview.AppendLine(_navigationPresenter.GetText("food.photo.preview.servings", locale, pending.Servings));
        preview.AppendLine();
        preview.AppendLine(_navigationPresenter.GetText("food.photo.preview.confirm", locale));

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
                _navigationPresenter.GetText("food.photo.expired", locale),
                InlineKeyboard(_navigationPresenter.BuildWeeklyMenuKeyboard(locale)));
        }

        if (_foodTrackingService is null)
        {
            return new TelegramRouteResponse(
                "food.photo.error",
                _navigationPresenter.GetText("food.photo.error", locale),
                InlineKeyboard(_navigationPresenter.BuildWeeklyMenuKeyboard(locale)));
        }

        await _foodTrackingService.LogQuickMealAsync(pending.MealName, pending.EstimatedCalories, pending.Servings, cancellationToken);

        return new TelegramRouteResponse(
            "food.photo.logged",
            _navigationPresenter.GetText("food.photo.logged", locale, pending.MealName, pending.EstimatedCalories, pending.Servings),
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
            _navigationPresenter.GetText("food.photo.cancelled", locale),
            InlineKeyboard(_navigationPresenter.BuildWeeklyMenuKeyboard(locale)));
    }

    private string BuildShoppingQuantitySuffix(string? quantity, string locale)
        => string.IsNullOrWhiteSpace(quantity)
            ? string.Empty
            : _navigationPresenter.GetText("food.shop.qty_suffix", locale, quantity);

    private string BuildShoppingStoreSuffix(string? store, string locale)
        => string.IsNullOrWhiteSpace(store)
            ? string.Empty
            : _navigationPresenter.GetText("food.shop.store_suffix", locale, store);

    private static string BuildInventoryQuantitySuffix(FoodItemDto item)
    {
        if (item.CurrentQuantity.HasValue)
        {
            return $" [{item.CurrentQuantity.Value.ToString("0.##", CultureInfo.InvariantCulture)}]";
        }

        return string.IsNullOrWhiteSpace(item.Quantity)
            ? string.Empty
            : $" [{item.Quantity}]";
    }

    private string BuildInventoryCatalogText(IReadOnlyList<FoodItemDto> items, string locale)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{_navigationPresenter.GetText("menu.inventory.title", locale)} ({items.Count}):");
        sb.AppendLine();

        var groups = items
            .GroupBy(item => string.IsNullOrWhiteSpace(item.Category)
                ? _navigationPresenter.GetText("inventory.category.uncategorized", locale)
                : item.Category!.Trim())
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            sb.AppendLine(BuildInventoryCategoryTitle(group.Key));
            foreach (var item in group.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
            {
                sb.AppendLine($"  [{item.Id}] {BuildInventoryItemTitle(item)}{BuildInventoryQuantitySuffix(item)}");
            }

            sb.AppendLine();
        }

        sb.AppendLine(EnsureInfoMarker(_navigationPresenter.GetText("inventory.add_to_cart_hint", locale)));
        return sb.ToString().TrimEnd();
    }

    private string BuildInventoryListText(IReadOnlyList<FoodItemDto> items, string locale, bool showMissing)
    {
        var available = items
            .Where(IsInventoryItemAvailable)
            .ToList();

        var filtered = showMissing
            ? items.Where(item => !IsInventoryItemAvailable(item)).ToList()
            : available;

        var sb = new StringBuilder();
        var header = showMissing
            ? _navigationPresenter.GetText("inventory.list.header.missing", locale, filtered.Count, items.Count)
            : _navigationPresenter.GetText("inventory.list.header.available", locale, available.Count, items.Count);
        sb.AppendLine(header);
        sb.AppendLine();

        if (filtered.Count == 0)
        {
            var emptyText = showMissing
                ? _navigationPresenter.GetText("inventory.list.empty.missing", locale)
                : _navigationPresenter.GetText("inventory.list.empty.available", locale);
            sb.AppendLine(EnsureInfoMarker(emptyText));
            sb.AppendLine();
        }
        else
        {
            var groups = filtered
                .GroupBy(item => string.IsNullOrWhiteSpace(item.Category)
                    ? _navigationPresenter.GetText("inventory.category.uncategorized", locale)
                    : item.Category!.Trim())
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var group in groups)
            {
                sb.AppendLine(BuildInventoryCategoryTitle(group.Key));
                foreach (var item in group.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
                {
                    sb.AppendLine($"  [{item.Id}] {BuildInventoryItemTitle(item)}{BuildInventoryQuantitySuffix(item)}");
                }

                sb.AppendLine();
            }
        }

        sb.AppendLine(EnsureQuestionMarker(showMissing
            ? _navigationPresenter.GetText("inventory.list.prompt.available", locale)
            : _navigationPresenter.GetText("inventory.list.prompt.missing", locale)));
        return sb.ToString().TrimEnd();
    }

    private TelegramInlineKeyboardMarkup BuildInventoryListKeyboard(string locale, bool showingMissing)
    {
        var toggleButton = showingMissing
            ? new TelegramInlineKeyboardButton(
                _navigationPresenter.GetText("inventory.list.button.available", locale),
                CallbackDataConstants.Inventory.ListAvailable)
            : new TelegramInlineKeyboardButton(
                _navigationPresenter.GetText("inventory.list.button.missing", locale),
                CallbackDataConstants.Inventory.ListMissing);

        var baseRows = _navigationPresenter.BuildInventoryKeyboard(locale).InlineKeyboard;
        var rows = new List<IReadOnlyList<TelegramInlineKeyboardButton>>(baseRows.Count + 1)
        {
            new[] { toggleButton }
        };
        rows.AddRange(baseRows);
        return new TelegramInlineKeyboardMarkup(rows);
    }

    private static bool IsInventoryItemAvailable(FoodItemDto item)
        => item.CurrentQuantity.GetValueOrDefault() > 0;

    private static string BuildInventoryCategoryTitle(string category)
    {
        if (CategoryEmojis.TryGetValue(category, out var emoji))
        {
            return $"{emoji} {category}";
        }

        return category;
    }

    private static string BuildInventoryItemTitle(FoodItemDto item)
        => BuildInventoryItemTitle(item.Name, item.IconEmoji);

    private static string BuildInventoryItemTitle(string name, string? iconEmoji)
    {
        if (!string.IsNullOrWhiteSpace(iconEmoji))
        {
            return $"{iconEmoji} {name}";
        }

        return $"📦 {name}";
    }

    private static string BuildUnknownDisplayName(PendingInventoryPhotoUnknown entry)
    {
        var original = entry.Name;
        if (string.IsNullOrWhiteSpace(entry.NameEn))
        {
            return $"🔹{original}";
        }

        var english = entry.NameEn;
        return $"🔹\"{english}\" ({original})";
    }

    private string BuildInventoryPhotoPreviewText(
        string locale,
        TelegramInventoryPhotoMode mode,
        IReadOnlyList<PendingInventoryPhotoCandidate> candidates,
        IReadOnlyList<PendingInventoryPhotoUnknown> unknown,
        PendingInventoryPhotoSession? session = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine(_navigationPresenter.GetText("inventory.photo.preview.title", locale, candidates.Count));
        sb.AppendLine(mode == TelegramInventoryPhotoMode.Consumption
            ? _navigationPresenter.GetText("inventory.photo.preview.mode.consume", locale)
            : _navigationPresenter.GetText("inventory.photo.preview.mode.restock", locale));

        if (session is { DetectedStoreNameEn: not null })
        {
            sb.AppendLine();
            sb.AppendLine($"🏪 {_navigationPresenter.GetText("inventory.photo.preview.store", locale, session.DetectedStoreNameEn)}");
        }

        sb.AppendLine();

        foreach (var candidate in candidates)
        {
            var sign = mode == TelegramInventoryPhotoMode.Consumption ? "-" : "+";
            var quantity = candidate.Quantity.ToString("0.##", CultureInfo.InvariantCulture);
            var unit = string.IsNullOrWhiteSpace(candidate.Unit) ? string.Empty : $" {candidate.Unit}";
            var priceSuffix = candidate.PricePerUnit.HasValue
                ? $" 💰{candidate.PricePerUnit.Value:0}/{(string.IsNullOrWhiteSpace(candidate.Unit) ? "pcs" : candidate.Unit)}"
                : string.Empty;
            sb.AppendLine(_navigationPresenter.GetText(
                "inventory.photo.preview.item",
                locale,
                candidate.Number,
                BuildInventoryItemTitle(candidate.Name, candidate.IconEmoji),
                sign,
                $"{quantity}{unit}",
                candidate.Confidence) + priceSuffix);
        }

        if (unknown.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine(EnsureWarningMarker(_navigationPresenter.GetText("inventory.photo.preview.unknown_title", locale)));
            foreach (var entry in unknown)
            {
                var displayName = BuildUnknownDisplayName(entry);
                var quantity = entry.Quantity.ToString("0.##", CultureInfo.InvariantCulture);
                var unit = string.IsNullOrWhiteSpace(entry.Unit) ? string.Empty : $" {entry.Unit}";
                var priceSuffix = entry.PricePerUnit.HasValue
                    ? $" 💰{entry.PricePerUnit.Value:0}/{(string.IsNullOrWhiteSpace(entry.Unit) ? "pcs" : entry.Unit)}"
                    : string.Empty;
                sb.AppendLine(_navigationPresenter.GetText(
                    "inventory.photo.preview.unknown_item",
                    locale,
                    entry.Number,
                    displayName,
                    "+",
                    $"{quantity}{unit}",
                    entry.Confidence) + priceSuffix);
            }
        }

        if (session?.NonProducts is { Count: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine(EnsureWarningMarker(_navigationPresenter.GetText("inventory.photo.preview.non_products", locale)));
            for (var index = 0; index < session.NonProducts.Count; index++)
            {
                sb.AppendLine($"{index + 1}) 🔹{session.NonProducts[index]}");
            }
        }

        sb.AppendLine();
        sb.AppendLine(EnsureQuestionMarker(_navigationPresenter.GetText("inventory.photo.preview.confirm", locale)));
        return sb.ToString().TrimEnd();
    }

    private async Task<TelegramRouteResponse> ApplyInventoryPhotoCandidatesAsync(
        ConversationScope scope,
        string locale,
        string pendingKey,
        PendingInventoryPhotoSession session,
        IReadOnlyList<PendingInventoryPhotoCandidate> candidates,
        CancellationToken cancellationToken)
    {
        var foodTrackingService = _foodTrackingService;
        if (foodTrackingService is null)
        {
            return new TelegramRouteResponse(
                "inventory.unavailable",
                _navigationPresenter.GetText("stub.wip", locale),
                InlineKeyboard(_navigationPresenter.BuildInventoryKeyboard(locale)));
        }

        var summaryLines = new List<string>();
        foreach (var candidate in candidates.OrderBy(x => x.Number))
        {
            var signedDelta = session.Mode == TelegramInventoryPhotoMode.Consumption
                ? -candidate.Quantity
                : candidate.Quantity;

            var updated = await foodTrackingService.AdjustInventoryQuantityAsync(candidate.ItemId, signedDelta, cancellationToken);

            // Update price and store for matched inventory items (restock mode only).
            if (session.Mode == TelegramInventoryPhotoMode.Restock && candidate.PricePerUnit.HasValue)
            {
                await foodTrackingService.UpdateInventoryPriceAndStoreAsync(
                    candidate.ItemId, candidate.PricePerUnit, session.ResolvedStoreName, cancellationToken);
            }

            var quantityText = updated.CurrentQuantity?.ToString("0.##", CultureInfo.InvariantCulture) ?? "0";
            summaryLines.Add(_navigationPresenter.GetText("inventory.photo.applied.item", locale, updated.Name, quantityText, signedDelta > 0 ? "+" : "-"));
        }

        var summary = new StringBuilder();
        summary.AppendLine($"✅ {_navigationPresenter.GetText("inventory.photo.applied", locale, summaryLines.Count)}");
        summary.AppendLine();
        foreach (var line in summaryLines)
        {
            summary.AppendLine(line);
        }

        // Check if store needs resolution.
        if (session.Mode == TelegramInventoryPhotoMode.Restock
            && session.ResolvedStoreName is null
            && !string.IsNullOrWhiteSpace(session.DetectedStoreNameEn))
        {
            var storeAlias = await foodTrackingService.ResolveStoreAliasAsync(session.DetectedStoreNameEn, cancellationToken);
            if (!string.IsNullOrWhiteSpace(storeAlias))
            {
                _pendingStateStore.InventoryPhotoSessions[pendingKey] = session with { ResolvedStoreName = storeAlias };
                return await TransitionToUnknownItemsOrFinishAsync(scope, locale, pendingKey, cancellationToken, summary);
            }

            var existingStores = await foodTrackingService.GetDistinctStoresAsync(cancellationToken);
            var matchFound = existingStores.Any(s => string.Equals(s, session.DetectedStoreNameEn, StringComparison.OrdinalIgnoreCase));

            if (matchFound)
            {
                // Auto-resolve to the matched existing store.
                var matched = existingStores.First(s => string.Equals(s, session.DetectedStoreNameEn, StringComparison.OrdinalIgnoreCase));
                _pendingStateStore.InventoryPhotoSessions[pendingKey] = session with { ResolvedStoreName = matched };
                await foodTrackingService.SaveStoreAliasAsync(session.DetectedStoreNameEn, matched, cancellationToken);

                // Update store for all applied candidates.
                foreach (var candidate in candidates)
                {
                    await foodTrackingService.UpdateInventoryPriceAndStoreAsync(
                        candidate.ItemId, null, matched, cancellationToken);
                }

                return await TransitionToUnknownItemsOrFinishAsync(scope, locale, pendingKey, cancellationToken, summary);
            }

            // Store not found — ask user.
            summary.AppendLine();
            summary.AppendLine($"🏪 {_navigationPresenter.GetText("inventory.photo.store.detected", locale, session.DetectedStoreName ?? session.DetectedStoreNameEn, session.DetectedStoreNameEn)}");

            _pendingStateStore.ChatActions[pendingKey] = PendingChatActionKind.InventoryPhotoAwaitingStoreResolution;
            return new TelegramRouteResponse(
                "inventory.photo.store.resolve",
                summary.ToString().TrimEnd(),
                InlineKeyboard(_navigationPresenter.BuildPhotoStoreResolutionKeyboard(locale, session.DetectedStoreNameEn)));
        }

        return await TransitionToUnknownItemsOrFinishAsync(scope, locale, pendingKey, cancellationToken, summary);
    }

    private async Task<TelegramRouteResponse> TransitionToUnknownItemsOrFinishAsync(
        ConversationScope scope,
        string locale,
        string pendingKey,
        CancellationToken cancellationToken,
        StringBuilder? existingSummary = null)
    {
        var foodTrackingService = _foodTrackingService;
        if (foodTrackingService is null)
        {
            return new TelegramRouteResponse(
                "inventory.unavailable",
                _navigationPresenter.GetText("stub.wip", locale),
                InlineKeyboard(_navigationPresenter.BuildInventoryKeyboard(locale)));
        }

        if (!_pendingStateStore.InventoryPhotoSessions.TryGetValue(pendingKey, out var session))
        {
            _pendingStateStore.ChatActions.TryRemove(pendingKey, out _);
            return ExpiredPhotoSession(locale);
        }

        // If store was resolved, update all applied candidates.
        if (session.ResolvedStoreName is not null && session.Candidates.Count > 0)
        {
            foreach (var candidate in session.Candidates)
            {
                try
                {
                    await foodTrackingService.UpdateInventoryPriceAndStoreAsync(
                        candidate.ItemId, null, session.ResolvedStoreName, cancellationToken);
                }
                catch (InvalidOperationException)
                {
                    // Item may have been deleted — skip silently.
                }
            }
        }

        if (session.Unknown.Count > 0)
        {
            var sb = existingSummary ?? new StringBuilder();
            sb.AppendLine();
            sb.AppendLine($"📦 {_navigationPresenter.GetText("inventory.photo.unknown.offer_title", locale, session.Unknown.Count)}");
            foreach (var entry in session.Unknown)
            {
                var displayName = BuildUnknownDisplayName(entry);
                var quantity = entry.Quantity.ToString("0.##", CultureInfo.InvariantCulture);
                var unit = string.IsNullOrWhiteSpace(entry.Unit) ? string.Empty : $" {entry.Unit}";
                var priceSuffix = entry.PricePerUnit.HasValue
                    ? $", 💰{entry.PricePerUnit.Value:0}/{(string.IsNullOrWhiteSpace(entry.Unit) ? "pcs" : entry.Unit)}"
                    : string.Empty;
                sb.AppendLine($"{entry.Number}) {displayName} — {quantity}{unit}{priceSuffix}");
            }
            sb.AppendLine();
            sb.AppendLine(EnsureQuestionMarker(_navigationPresenter.GetText("inventory.photo.unknown.offer_prompt", locale)));

            _pendingStateStore.ChatActions[pendingKey] = PendingChatActionKind.InventoryPhotoAwaitingUnknownSelection;
            return new TelegramRouteResponse(
                "inventory.photo.unknown.offer",
                sb.ToString().TrimEnd(),
                InlineKeyboard(_navigationPresenter.BuildPhotoUnknownItemsKeyboard(locale)));
        }

        // No unknown items — clean up and finish.
        _pendingStateStore.ChatActions.TryRemove(pendingKey, out _);
        _pendingStateStore.InventoryPhotoSessions.TryRemove(pendingKey, out _);

        if (existingSummary is not null)
        {
            return new TelegramRouteResponse(
                "inventory.photo.applied",
                existingSummary.ToString().TrimEnd(),
                InlineKeyboard(_navigationPresenter.BuildInventoryKeyboard(locale)));
        }

        return new TelegramRouteResponse(
            "inventory.photo.done",
            EnsureInfoMarker(_navigationPresenter.GetText("inventory.photo.done", locale)),
            InlineKeyboard(_navigationPresenter.BuildInventoryKeyboard(locale)));
    }

    private async Task<TelegramRouteResponse> AddUnknownItemsToInventoryAsync(
        string locale,
        string pendingKey,
        PendingInventoryPhotoSession session,
        IReadOnlyList<PendingInventoryPhotoUnknown> selectedUnknowns,
        CancellationToken cancellationToken)
    {
        var foodTrackingService = _foodTrackingService;
        if (foodTrackingService is null)
        {
            return new TelegramRouteResponse(
                "inventory.unavailable",
                _navigationPresenter.GetText("stub.wip", locale),
                InlineKeyboard(_navigationPresenter.BuildInventoryKeyboard(locale)));
        }

        var addedLines = new List<string>();
        foreach (var unknown in selectedUnknowns.OrderBy(u => u.Number))
        {
            // Inventory item names must remain in English; skip entries without a resolved English name.
            if (string.IsNullOrWhiteSpace(unknown.NameEn))
            {
                continue;
            }

            var created = await foodTrackingService.AddInventoryItemAsync(
                unknown.NameEn.Trim(),
                session.ResolvedStoreName,
                unknown.PricePerUnit,
                unknown.Quantity,
                unknown.Category,
                cancellationToken: cancellationToken);
            addedLines.Add($"  {BuildInventoryItemTitle(created)}" + (created.Price.HasValue ? $" 💰{created.Price.Value:0}" : string.Empty));
        }

        _pendingStateStore.ChatActions.TryRemove(pendingKey, out _);
        _pendingStateStore.InventoryPhotoSessions.TryRemove(pendingKey, out _);

        if (addedLines.Count == 0)
        {
            return new TelegramRouteResponse(
                "inventory.photo.unknown.added.none",
                EnsureWarningMarker(_navigationPresenter.GetText("inventory.photo.unknown.added.none", locale)),
                InlineKeyboard(_navigationPresenter.BuildInventoryKeyboard(locale)));
        }

        var sb = new StringBuilder();
        sb.AppendLine($"✅ {_navigationPresenter.GetText("inventory.photo.unknown.added", locale, addedLines.Count)}");
        foreach (var line in addedLines)
        {
            sb.AppendLine(line);
        }

        return new TelegramRouteResponse(
            "inventory.photo.unknown.added",
            sb.ToString().TrimEnd(),
            InlineKeyboard(_navigationPresenter.BuildInventoryKeyboard(locale)));
    }

    private TelegramRouteResponse ExpiredPhotoSession(string locale)
    {
        return new TelegramRouteResponse(
            "inventory.photo.expired",
            EnsureWarningMarker(_navigationPresenter.GetText("inventory.photo.expired", locale)),
            InlineKeyboard(_navigationPresenter.BuildInventoryKeyboard(locale)));
    }

    private string BuildCaloriesPerServingSuffix(int? caloriesPerServing, string locale)
        => caloriesPerServing.HasValue
            ? _navigationPresenter.GetText("food.weekly.view.calories_suffix", locale, caloriesPerServing.Value)
            : string.Empty;

    /// <summary>
    /// Parses inventory cart selection in formats:
    /// "45 2" — plain ID + optional quantity
    /// "[45] 🥛 Milk 2" — bracketed ID (copied from catalog) + optional trailing quantity
    /// </summary>
    internal static bool TryParseInventoryCartSelection(
        string input,
        out int itemId,
        out string? quantity)
    {
        itemId = 0;
        quantity = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var trimmed = input.Trim();

        // Try bracketed format first: [45] ... trailing-quantity
        if (trimmed.StartsWith('['))
        {
            var closeBracket = trimmed.IndexOf(']');
            if (closeBracket > 1
                && int.TryParse(trimmed.AsSpan(1, closeBracket - 1), NumberStyles.None, CultureInfo.InvariantCulture, out itemId)
                && itemId > 0)
            {
                // Everything after "]" — extract trailing quantity token (last word if it looks numeric)
                var tail = trimmed[(closeBracket + 1)..].Trim();
                if (tail.Length > 0)
                {
                    var lastSpace = tail.LastIndexOf(' ');
                    var lastToken = lastSpace >= 0 ? tail[(lastSpace + 1)..] : tail;
                    if (InventoryQuantityTokenRegex.IsMatch(lastToken))
                    {
                        quantity = lastToken;
                    }
                }

                return true;
            }
        }

        // Plain format: 45 2kg
        var parts = trimmed.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out itemId) || itemId <= 0)
        {
            return false;
        }

        quantity = parts.Length > 1 ? parts[1].Trim() : null;
        return true;
    }

    private static bool TryParseInventoryMinQuantityUpdates(
        string input,
        out IReadOnlyList<InventoryQuantityUpdate> updates)
    {
        updates = [];
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var parsed = new List<InventoryQuantityUpdate>();
        foreach (var token in SplitBatchInputTokens(input))
        {
            if (TryParseInventoryLeadingIdToken(token, out var itemId, out var tail))
            {
                if (TryParseInventoryOperatorValue(
                        tail,
                        allowSetWithoutOperator: true,
                        out var mode,
                        out var parsedValue,
                        out var hasMalformedOperation))
                {
                    parsed.Add(new InventoryQuantityUpdate(itemId, mode, parsedValue));
                    continue;
                }

                if (hasMalformedOperation)
                {
                    updates = [];
                    return false;
                }

                // Unchanged line (typically copied from "All items").
                continue;
            }
        }

        if (parsed.Count == 0)
        {
            return false;
        }

        updates = parsed;
        return true;
    }

    private static bool TryParseInventoryUnknownItemLinks(
        string input,
        out IReadOnlyList<(int UnknownNumber, int ItemId)> links)
    {
        links = [];
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var parsed = new List<(int UnknownNumber, int ItemId)>();
        var tokens = input.Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var token in tokens)
        {
            var pair = token.Split('=', 2, StringSplitOptions.TrimEntries);
            if (pair.Length != 2)
            {
                return false;
            }

            if (!int.TryParse(pair[0], NumberStyles.None, CultureInfo.InvariantCulture, out var unknownNumber) || unknownNumber <= 0)
            {
                return false;
            }

            if (!int.TryParse(pair[1], NumberStyles.None, CultureInfo.InvariantCulture, out var itemId) || itemId <= 0)
            {
                return false;
            }

            parsed.Add((unknownNumber, itemId));
        }

        if (parsed.Count == 0)
        {
            return false;
        }

        links = parsed;
        return true;
    }

    private static bool TryParseInventoryQuantityUpdates(
        string input,
        out IReadOnlyList<InventoryQuantityUpdate> updates)
    {
        updates = [];
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var parsed = new List<InventoryQuantityUpdate>();
        foreach (var token in SplitBatchInputTokens(input))
        {
            if (TryParseInventoryLeadingIdToken(token, out var itemId, out var tail))
            {
                if (TryParseInventoryOperatorValue(
                        tail,
                        allowSetWithoutOperator: false,
                        out var mode,
                        out var parsedValue,
                        out var hasMalformedOperation))
                {
                    parsed.Add(new InventoryQuantityUpdate(itemId, mode, parsedValue));
                    continue;
                }

                if (hasMalformedOperation)
                {
                    updates = [];
                    return false;
                }

                // Unchanged line (typically copied from "All items").
                continue;
            }
        }

        if (parsed.Count == 0)
        {
            return false;
        }

        updates = parsed;
        return true;
    }

    private static bool TryParseInventoryLeadingIdToken(
        string token,
        out int itemId,
        out string tail)
    {
        itemId = 0;
        tail = string.Empty;

        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var match = InventoryLeadingIdRegex.Match(token.Trim());
        if (!match.Success)
        {
            return false;
        }

        if (!int.TryParse(match.Groups["id"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out itemId) || itemId <= 0)
        {
            return false;
        }

        tail = match.Groups["tail"].Value;
        return true;
    }

    private static bool TryParseInventoryOperatorValue(
        string tail,
        bool allowSetWithoutOperator,
        out InventoryQuantityUpdateMode mode,
        out decimal value,
        out bool hasMalformedOperation)
    {
        mode = InventoryQuantityUpdateMode.Delta;
        value = 0m;
        hasMalformedOperation = false;

        if (string.IsNullOrWhiteSpace(tail))
        {
            return false;
        }

        var normalizedTail = tail.Trim();
        var operatorMatch = InventoryTrailingOperatorRegex.Match(normalizedTail);
        if (operatorMatch.Success)
        {
            if (!TryParseInvariantDecimal(operatorMatch.Groups["value"].Value, out var parsedRaw))
            {
                hasMalformedOperation = true;
                return false;
            }

            var op = operatorMatch.Groups["op"].Value;
            if (op == "=")
            {
                if (parsedRaw < 0m)
                {
                    hasMalformedOperation = true;
                    return false;
                }

                mode = InventoryQuantityUpdateMode.Set;
                value = parsedRaw;
                return true;
            }

            if (parsedRaw <= 0m)
            {
                hasMalformedOperation = true;
                return false;
            }

            mode = InventoryQuantityUpdateMode.Delta;
            value = op == "-" ? -parsedRaw : parsedRaw;
            return true;
        }

        if (!allowSetWithoutOperator)
        {
            hasMalformedOperation = InventoryPotentialOperationHintRegex.IsMatch(normalizedTail);
            return false;
        }

        var plainValueMatch = InventoryTrailingPlainValueRegex.Match(normalizedTail);
        if (!plainValueMatch.Success)
        {
            hasMalformedOperation = InventoryPotentialOperationHintRegex.IsMatch(normalizedTail);
            return false;
        }

        if (!TryParseInvariantDecimal(plainValueMatch.Groups["value"].Value, out var parsedValue) || parsedValue < 0m)
        {
            hasMalformedOperation = true;
            return false;
        }

        mode = InventoryQuantityUpdateMode.Set;
        value = parsedValue;
        return true;
    }

    private static bool TryParseInvariantDecimal(string raw, out decimal value)
    {
        var normalized = raw.Replace(',', '.');
        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    private static IReadOnlyList<string> SplitBatchInputTokens(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return [];
        }

        var split = input
            .Split(['\r', '\n', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(static token => token.Trim())
            .Where(static token => !string.IsNullOrWhiteSpace(token))
            .ToList();

        return split.Count == 0 ? [] : split;
    }

    private sealed record InventoryQuantityUpdate(int ItemId, InventoryQuantityUpdateMode Mode, decimal Value);

    private enum InventoryQuantityUpdateMode
    {
        Delta = 0,
        Set = 1
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
        string? FollowUpMainKeyboardLocale = null,
        bool SuppressChatMessage = false);
}

