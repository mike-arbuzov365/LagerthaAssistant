using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
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

    private static readonly ConcurrentDictionary<string, GraphDeviceLoginChallenge> PendingGraphChallenges = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, PendingVocabularySaveRequest> PendingVocabularySaves = new(StringComparer.Ordinal);

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

        var callbackQueryId = update?.CallbackQuery?.Id;

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
                return await HandleCallbackAsync(route.CallbackData!, scope, locale, currentSection, cancellationToken);

            case NavigationRouteKind.VocabularyText:
                {
                    var result = await _orchestrator.ProcessAsync(
                        inbound.Text,
                        scope.Channel,
                        scope.UserId,
                        scope.ConversationId,
                        cancellationToken);
                    return await BuildVocabularyTextResponseAsync(result, scope, locale, cancellationToken);
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
        ConversationScope scope,
        string locale,
        string currentSection,
        CancellationToken cancellationToken)
    {
        if (string.Equals(callbackData, CallbackDataConstants.Nav.Main, StringComparison.Ordinal))
        {
            await _navigationStateService.SetCurrentSectionAsync(scope.Channel, scope.UserId, scope.ConversationId, NavigationSections.Main, cancellationToken);
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
            return await HandleOneDriveCallbackAsync(callbackData, scope, locale, cancellationToken);
        }

        if (callbackData.StartsWith(CallbackDataConstants.Vocab.Prefix, StringComparison.Ordinal))
        {
            await _navigationStateService.SetCurrentSectionAsync(scope.Channel, scope.UserId, scope.ConversationId, NavigationSections.Vocabulary, cancellationToken);

            return callbackData switch
            {
                CallbackDataConstants.Vocab.Add => new TelegramRouteResponse(
                    "vocab.add",
                    _navigationPresenter.GetText("vocab.add.prompt", locale),
                    InlineKeyboard(_navigationPresenter.BuildVocabularyKeyboard(locale))),
                CallbackDataConstants.Vocab.List => await BuildVocabularyListResponseAsync(locale, cancellationToken),
                CallbackDataConstants.Vocab.Url => new TelegramRouteResponse(
                    "vocab.url",
                    _navigationPresenter.GetText("vocab.url.prompt", locale),
                    InlineKeyboard(_navigationPresenter.BuildVocabularyKeyboard(locale))),
                CallbackDataConstants.Vocab.Batch => BuildBatchModeResponse(locale),
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
                        _navigationPresenter.GetText("onedrive.still_not_signed_in", locale),
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
                        _navigationPresenter.GetText("onedrive.still_not_signed_in", locale),
                        Environment.NewLine,
                        Environment.NewLine,
                        expiredScreen.Text)
                };
            }

            var complete = await _graphAuthService.CompleteLoginAsync(challenge, cancellationToken);
            if (complete.Succeeded)
            {
                PendingGraphChallenges.TryRemove(challengeKey, out _);
                await _storagePreferenceService.SetModeAsync(scope, VocabularyStorageMode.Graph, cancellationToken);
                _storageModeProvider.SetMode(VocabularyStorageMode.Graph);
            }

            var includeCheckButton = !complete.Succeeded;
            var screen = await BuildOneDriveResponseAsync(scope, locale, includeCheckStatusButton: includeCheckButton, cancellationToken);

            if (complete.Succeeded)
            {
                return screen with
                {
                    Intent = "settings.onedrive.check.success",
                    Text = string.Concat(
                        _navigationPresenter.GetText("onedrive.login_switched_to_graph", locale),
                        Environment.NewLine,
                        Environment.NewLine,
                        screen.Text)
                };
            }

            return screen with
            {
                Intent = "settings.onedrive.check.pending",
                Text = string.Concat(
                    _navigationPresenter.GetText("onedrive.still_not_signed_in", locale),
                    Environment.NewLine,
                    Environment.NewLine,
                    WebUtility.HtmlEncode(complete.Message),
                    Environment.NewLine,
                    Environment.NewLine,
                    screen.Text)
            };
        }

        return await BuildOneDriveResponseAsync(scope, locale, includeCheckStatusButton: false, cancellationToken);
    }

    private async Task<TelegramRouteResponse> BuildVocabularyTextResponseAsync(
        ConversationAgentResult result,
        ConversationScope scope,
        string locale,
        CancellationToken cancellationToken)
    {
        var formatted = _responseFormatter.Format(result);
        var pendingKey = BuildPendingSaveKey(scope);
        PendingVocabularySaves.TryRemove(pendingKey, out _);

        if (result.Items.Count == 0)
        {
            return new TelegramRouteResponse(result.Intent, formatted);
        }

        var saveMode = await _saveModePreferenceService.GetModeAsync(scope, cancellationToken);
        var saveCandidates = BuildSaveCandidates(result.Items);
        var previewWarnings = BuildPreviewWarnings(result.Items);

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

        var keepPending = appendResult.Status == VocabularyAppendStatus.Error;
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
                || item.AssistantCompletion is null
                || item.AppendPreview is null
                || item.AppendPreview.Status != VocabularyAppendPreviewStatus.ReadyToAppend
                || string.IsNullOrWhiteSpace(item.AppendPreview.TargetDeckFileName))
            {
                continue;
            }

            candidates.Add(new PendingVocabularySaveRequest(
                item.Input,
                item.AssistantCompletion.Content,
                item.AppendPreview.TargetDeckFileName,
                OverridePartOfSpeech: null));
        }

        return candidates;
    }

    private static string BuildPreviewWarnings(IReadOnlyList<ConversationAgentItemResult> items)
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

            warnings.Add($"⚠️ {item.AppendPreview.Message}");
        }

        return string.Join(Environment.NewLine, warnings.Distinct(StringComparer.Ordinal));
    }

    private string BuildAppendStatusMessage(VocabularyAppendResult appendResult, string locale)
    {
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
                appendResult.Message ?? "Unknown error")
        };
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

    private async Task<TelegramRouteResponse> BuildVocabularySectionResponseAsync(string locale, CancellationToken cancellationToken)
    {
        var count = await _vocabularyCardRepository.CountAllAsync(cancellationToken);
        var title = _navigationPresenter.GetText("menu.vocabulary.title", locale, count);

        return new TelegramRouteResponse(
            "vocab.section",
            title,
            InlineKeyboard(_navigationPresenter.BuildVocabularyKeyboard(locale)));
    }

    private async Task<TelegramRouteResponse> BuildVocabularyListResponseAsync(string locale, CancellationToken cancellationToken)
    {
        var recent = await _vocabularyCardRepository.GetRecentAsync(10, cancellationToken);
        if (recent.Count == 0)
        {
            return new TelegramRouteResponse(
                "vocab.list",
                _navigationPresenter.GetText("vocab.list.empty", locale),
                InlineKeyboard(_navigationPresenter.BuildVocabularyKeyboard(locale)));
        }

        var lines = new List<string>
        {
            _navigationPresenter.GetText("vocab.list.title", locale)
        };

        for (var i = 0; i < recent.Count; i++)
        {
            var pos = string.IsNullOrWhiteSpace(recent[i].PartOfSpeechMarker)
                ? string.Empty
                : $" ({WebUtility.HtmlEncode(recent[i].PartOfSpeechMarker)})";
            lines.Add($"{i + 1}) {WebUtility.HtmlEncode(recent[i].Word)}{pos}");
        }

        return new TelegramRouteResponse(
            "vocab.list",
            string.Join(Environment.NewLine, lines),
            InlineKeyboard(_navigationPresenter.BuildVocabularyKeyboard(locale)));
    }

    private TelegramRouteResponse BuildBatchModeResponse(string locale)
    {
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

        var text = string.Join(Environment.NewLine, new[]
        {
            _navigationPresenter.GetText("settings.title", locale),
            string.Empty,
            $"{_navigationPresenter.GetText("settings.language", locale)}: {_navigationPresenter.GetLanguageDisplayName(locale)}",
            $"{_navigationPresenter.GetText("settings.save_mode", locale)}: <b>{WebUtility.HtmlEncode(_saveModePreferenceService.ToText(saveMode))}</b>",
            $"{_navigationPresenter.GetText("settings.storage_mode", locale)}: <b>{WebUtility.HtmlEncode(_storageModeProvider.ToText(storageMode))}</b>",
            $"{_navigationPresenter.GetText("settings.onedrive", locale)}: {WebUtility.HtmlEncode(_navigationPresenter.GetText(graphStatus.IsAuthenticated ? "onedrive.status_connected" : "onedrive.status_disconnected", locale))}",
            _navigationPresenter.GetText("settings.notion", locale)
        });

        return new TelegramRouteResponse(
            "settings.section",
            text,
            InlineKeyboard(_navigationPresenter.BuildSettingsKeyboard(locale)),
            IsHtml: true);
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
            lines.Add(WebUtility.HtmlEncode(status.Message));
        }

        return new TelegramRouteResponse(
            "settings.onedrive",
            string.Join(Environment.NewLine, lines),
            InlineKeyboard(_navigationPresenter.BuildOneDriveKeyboard(locale, status.IsAuthenticated, includeCheckStatusButton)),
            IsHtml: true);
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

    private sealed record PendingVocabularySaveRequest(
        string RequestedWord,
        string AssistantReply,
        string TargetDeckFileName,
        string? OverridePartOfSpeech);

    private sealed record TelegramRouteResponse(
        string Intent,
        string Text,
        TelegramSendOptions? Options = null,
        bool IsHtml = false,
        string? FollowUpMainKeyboardLocale = null);
}
