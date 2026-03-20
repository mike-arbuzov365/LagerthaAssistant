using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using LagerthaAssistant.Api.Contracts;
using LagerthaAssistant.Api.Interfaces;
using LagerthaAssistant.Api.Options;
using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.Interfaces.AI;
using LagerthaAssistant.Application.Interfaces.Agents;
using LagerthaAssistant.Application.Interfaces;
using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Interfaces.Repositories;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.Application.Navigation;
using LagerthaAssistant.Application.Services.Vocabulary;
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

    private static readonly ConcurrentDictionary<string, GraphDeviceLoginChallenge> PendingGraphChallenges = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, PendingVocabularySaveRequest> PendingVocabularySaves = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, PendingVocabularyUrlSession> PendingVocabularyUrlSessions = new(StringComparer.Ordinal);

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
    private readonly VocabularyDeckOptions _vocabularyDeckOptions;
    private readonly IGraphAuthService _graphAuthService;
    private readonly ITelegramNavigationPresenter _navigationPresenter;
    private readonly ITelegramConversationResponseFormatter _responseFormatter;
    private readonly ITelegramBotSender _telegramBotSender;
    private readonly ITelegramProcessedUpdateRepository _processedUpdates;
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
        VocabularyDeckOptions vocabularyDeckOptions,
        IGraphAuthService graphAuthService,
        ITelegramNavigationPresenter navigationPresenter,
        ITelegramConversationResponseFormatter responseFormatter,
        ITelegramBotSender telegramBotSender,
        ITelegramProcessedUpdateRepository processedUpdates,
        IOptions<TelegramOptions> options,
        ILogger<TelegramController> logger)
    {
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
        _vocabularyDeckOptions = vocabularyDeckOptions;
        _graphAuthService = graphAuthService;
        _navigationPresenter = navigationPresenter;
        _responseFormatter = responseFormatter;
        _telegramBotSender = telegramBotSender;
        _processedUpdates = processedUpdates;
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
                await _navigationStateService.SetCurrentSectionAsync(scope.Channel, scope.UserId, scope.ConversationId, NavigationSections.Main, cancellationToken);
                return new TelegramRouteResponse(
                    "nav.main.chat",
                    _navigationPresenter.GetText("menu.main.title", locale),
                    ReplyKeyboard(_navigationPresenter.BuildMainReplyKeyboard(locale)));

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
                    var urlFlowResponse = await TryHandleVocabularyUrlFlowAsync(
                        inbound.Text,
                        scope,
                        locale,
                        cancellationToken);
                    if (urlFlowResponse is not null)
                    {
                        return urlFlowResponse;
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
                return new TelegramRouteResponse(
                    "shopping.stub",
                    _navigationPresenter.GetText("stub.wip", locale),
                    InlineKeyboard(_navigationPresenter.BuildShoppingKeyboard(locale)));

            case NavigationRouteKind.WeeklyMenuText:
                return new TelegramRouteResponse(
                    "weekly.stub",
                    _navigationPresenter.GetText("stub.wip", locale),
                    InlineKeyboard(_navigationPresenter.BuildWeeklyMenuKeyboard(locale)));

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
            PendingVocabularyUrlSessions.TryRemove(BuildPendingUrlSessionKey(scope), out _);
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
                CallbackDataConstants.Vocab.Url => HandleVocabularyUrlStartCallback(scope, locale),
                CallbackDataConstants.Vocab.UrlSelectAll => await HandleVocabularyUrlSelectAllAsync(scope, locale, cancellationToken),
                CallbackDataConstants.Vocab.UrlCancel => HandleVocabularyUrlCancelCallback(scope, locale),
                CallbackDataConstants.Vocab.Batch => BuildBatchModeResponse(scope, locale),
                CallbackDataConstants.Vocab.SaveYes => await HandleVocabularySaveConfirmationAsync(scope, locale, saveRequested: true, cancellationToken),
                CallbackDataConstants.Vocab.SaveNo => await HandleVocabularySaveConfirmationAsync(scope, locale, saveRequested: false, cancellationToken),
                _ => new TelegramRouteResponse(
                    "vocab.unknown",
                    _navigationPresenter.GetText("stub.wip", locale),
                    InlineKeyboard(_navigationPresenter.BuildVocabularyKeyboard(locale)))
            };
        }

        if (callbackData.StartsWith(CallbackDataConstants.Shop.Prefix, StringComparison.Ordinal))
        {
            await _navigationStateService.SetCurrentSectionAsync(scope.Channel, scope.UserId, scope.ConversationId, NavigationSections.Shopping, cancellationToken);
            return new TelegramRouteResponse(
                "shopping.stub",
                _navigationPresenter.GetText("stub.wip", locale),
                InlineKeyboard(_navigationPresenter.BuildShoppingKeyboard(locale)));
        }

        if (callbackData.StartsWith(CallbackDataConstants.Weekly.Prefix, StringComparison.Ordinal))
        {
            await _navigationStateService.SetCurrentSectionAsync(scope.Channel, scope.UserId, scope.ConversationId, NavigationSections.WeeklyMenu, cancellationToken);
            return new TelegramRouteResponse(
                "weekly.stub",
                _navigationPresenter.GetText("stub.wip", locale),
                InlineKeyboard(_navigationPresenter.BuildWeeklyMenuKeyboard(locale)));
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
            CallbackDataConstants.Settings.Notion => new TelegramRouteResponse(
                "settings.notion",
                _navigationPresenter.GetText("notion.title", locale),
                InlineKeyboard(_navigationPresenter.BuildNotionKeyboard(locale)),
                IsHtml: true),
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
            PendingGraphChallenges.TryRemove(BuildGraphChallengeKey(scope), out _);

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

            PendingGraphChallenges[BuildGraphChallengeKey(scope)] = start.Challenge;
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
            if (!PendingGraphChallenges.TryGetValue(challengeKey, out var challenge))
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
                PendingGraphChallenges.TryRemove(challengeKey, out _);
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
                PendingGraphChallenges.TryRemove(challengeKey, out _);
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
        PendingVocabularyUrlSessions.TryRemove(BuildPendingUrlSessionKey(scope), out _);

        return new TelegramRouteResponse(
            "vocab.add",
            _navigationPresenter.GetText("vocab.add.prompt", locale),
            InlineKeyboard(_navigationPresenter.BuildVocabularyKeyboard(locale)));
    }

    private TelegramRouteResponse HandleVocabularyUrlStartCallback(
        ConversationScope scope,
        string locale)
    {
        PendingVocabularyUrlSessions[BuildPendingUrlSessionKey(scope)] = PendingVocabularyUrlSession.AwaitingSource;

        return new TelegramRouteResponse(
            "vocab.url",
            _navigationPresenter.GetText("vocab.url.prompt", locale),
            InlineKeyboard(_navigationPresenter.BuildVocabularyKeyboard(locale)));
    }

    private TelegramRouteResponse HandleVocabularyUrlCancelCallback(
        ConversationScope scope,
        string locale)
    {
        PendingVocabularyUrlSessions.TryRemove(BuildPendingUrlSessionKey(scope), out _);

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
        if (!PendingVocabularyUrlSessions.TryGetValue(key, out var session)
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

    private async Task<TelegramRouteResponse?> TryHandleVocabularyUrlFlowAsync(
        string inboundText,
        ConversationScope scope,
        string locale,
        CancellationToken cancellationToken)
    {
        var normalizedInput = inboundText?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedInput))
        {
            return null;
        }

        var pendingKey = BuildPendingUrlSessionKey(scope);
        var hasSession = PendingVocabularyUrlSessions.TryGetValue(pendingKey, out var session);
        var shouldAutoStartFromUrl = !hasSession && UrlLikeRegex.IsMatch(normalizedInput);

        if (!hasSession && !shouldAutoStartFromUrl)
        {
            return null;
        }

        session ??= PendingVocabularyUrlSession.AwaitingSource;

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

                PendingVocabularyUrlSessions.TryRemove(pendingKey, out _);
                return null;
            }

            return await ProcessVocabularyUrlSelectionAsync(
                selection.SelectedWords,
                scope,
                locale,
                cancellationToken);
        }

        var discovery = await _vocabularyDiscoveryService.DiscoverAsync(normalizedInput, cancellationToken);
        if (discovery.Status == VocabularyDiscoveryStatus.InvalidSource
            || discovery.Status == VocabularyDiscoveryStatus.Failed)
        {
            PendingVocabularyUrlSessions[pendingKey] = PendingVocabularyUrlSession.AwaitingSource;

            return new TelegramRouteResponse(
                "vocab.url.invalid",
                _navigationPresenter.GetText("vocab.url.invalid", locale),
                InlineKeyboard(_navigationPresenter.BuildVocabularyKeyboard(locale)));
        }

        if (discovery.Status != VocabularyDiscoveryStatus.Success || discovery.Candidates.Count == 0)
        {
            PendingVocabularyUrlSessions.TryRemove(pendingKey, out _);
            return new TelegramRouteResponse(
                "vocab.url.empty",
                _navigationPresenter.GetText("vocab.url.empty", locale),
                InlineKeyboard(_navigationPresenter.BuildVocabularyKeyboard(locale)));
        }

        var orderedCandidates = OrderUrlCandidates(discovery.Candidates);
        PendingVocabularyUrlSessions[pendingKey] = new PendingVocabularyUrlSession(
            PendingVocabularyUrlStage.AwaitingSelection,
            orderedCandidates);

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

        PendingVocabularyUrlSessions.TryRemove(BuildPendingUrlSessionKey(scope), out _);
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

    private async Task<TelegramRouteResponse> BuildVocabularyTextResponseAsync(
        ConversationAgentResult result,
        ConversationScope scope,
        string locale,
        string rawInput,
        CancellationToken cancellationToken)
    {
        var formatted = _responseFormatter.Format(result);
        var pendingKey = BuildPendingSaveKey(scope);
        PendingVocabularySaves.TryRemove(pendingKey, out _);

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
                PendingVocabularySaves[pendingKey] = pending;

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
                formatted = AppendTextBlock(formatted, _navigationPresenter.GetText("vocab.save_batch_ask_hint", locale));
            }

            if (!string.IsNullOrWhiteSpace(previewWarnings))
            {
                formatted = AppendTextBlock(formatted, previewWarnings);
            }

            return new TelegramRouteResponse(result.Intent, formatted);
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
        if (!PendingVocabularySaves.TryGetValue(pendingKey, out var pending))
        {
            return new TelegramRouteResponse(
                "vocab.save.none",
                _navigationPresenter.GetText("vocab.no_pending_save", locale),
                InlineKeyboard(_navigationPresenter.BuildVocabularyKeyboard(locale)));
        }

        if (!saveRequested)
        {
            PendingVocabularySaves.TryRemove(pendingKey, out _);
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
            PendingVocabularySaves.TryRemove(pendingKey, out _);
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
        PendingVocabularyUrlSessions.TryRemove(BuildPendingUrlSessionKey(scope), out _);
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
        var oneDriveStatus = StripStatusPrefix(
            _navigationPresenter.GetText(
                graphStatus.IsAuthenticated ? "onedrive.status_connected" : "onedrive.status_disconnected",
                locale));

        var text = string.Join(Environment.NewLine, new[]
        {
            _navigationPresenter.GetText("settings.title", locale),
            string.Empty,
            $"• <b>{WebUtility.HtmlEncode(languageLabel)}:</b> {WebUtility.HtmlEncode(_navigationPresenter.GetLanguageDisplayName(locale))}",
            $"• <b>{WebUtility.HtmlEncode(saveModeLabel)}:</b> <b>{WebUtility.HtmlEncode(_saveModePreferenceService.ToText(saveMode))}</b>",
            $"• <b>{WebUtility.HtmlEncode(storageModeLabel)}:</b> <b>{WebUtility.HtmlEncode(_storageModeProvider.ToText(storageMode))}</b>",
            $"• <b>{WebUtility.HtmlEncode(oneDriveLabel)}:</b> {WebUtility.HtmlEncode(oneDriveStatus)}",
            $"• {WebUtility.HtmlEncode(notionLabel)}"
        });

        return new TelegramRouteResponse(
            "settings.section",
            text,
            InlineKeyboard(_navigationPresenter.BuildSettingsKeyboard(locale)),
            IsHtml: true);
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

    private static string BuildPendingUrlSessionKey(ConversationScope scope)
        => string.Concat(scope.Channel, ":", scope.UserId, ":", scope.ConversationId);

    private sealed record ConfiguredDeckTarget(
        string Marker,
        string DeckFileName);

    private sealed record PendingVocabularySaveRequest(
        string RequestedWord,
        string AssistantReply,
        string TargetDeckFileName,
        string? OverridePartOfSpeech);

    private sealed record PendingVocabularyUrlSession(
        PendingVocabularyUrlStage Stage,
        IReadOnlyList<PendingVocabularyUrlCandidate> Candidates)
    {
        public static PendingVocabularyUrlSession AwaitingSource { get; }
            = new(PendingVocabularyUrlStage.AwaitingSource, []);
    }

    private enum PendingVocabularyUrlStage
    {
        AwaitingSource = 0,
        AwaitingSelection = 1
    }

    private sealed record PendingVocabularyUrlCandidate(
        int Number,
        string Word,
        string PartOfSpeech,
        int Frequency);

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

