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

    private readonly IConversationOrchestrator _orchestrator;
    private readonly IAssistantSessionService _assistantSessionService;
    private readonly IConversationScopeAccessor _scopeAccessor;
    private readonly IVocabularyStorageModeProvider _storageModeProvider;
    private readonly IVocabularyStoragePreferenceService _storagePreferenceService;
    private readonly IUserLocaleStateService _userLocaleStateService;
    private readonly INavigationStateService _navigationStateService;
    private readonly NavigationRouter _navigationRouter;
    private readonly IVocabularyCardRepository _vocabularyCardRepository;
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
    [ProducesResponseType(typeof(TelegramWebhookResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<TelegramWebhookResponse>> Webhook(
        [FromBody] TelegramWebhookUpdateRequest update,
        CancellationToken cancellationToken = default)
    {
        if (!IsSecretValid())
        {
            return Unauthorized();
        }

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

        var applyMode = await ApiVocabularyStorageModeApplier.TryApplyAsync(
            _storageModeProvider,
            _storagePreferenceService,
            scope,
            requestedStorageMode: null,
            cancellationToken);
        if (!applyMode.Success)
        {
            return Ok(new TelegramWebhookResponse(false, false, null, applyMode.Error));
        }

        var localeState = await _userLocaleStateService.EnsureLocaleAsync(
            scope.Channel,
            scope.UserId,
            inbound.LanguageCode,
            inbound.IsCallback ? null : inbound.Text,
            cancellationToken);

        var currentSection = await _navigationStateService.GetCurrentSectionAsync(
            scope.Channel,
            scope.UserId,
            scope.ConversationId,
            cancellationToken);

        var labels = _navigationPresenter.GetMainMenuLabels(localeState.Locale);
        var route = _navigationRouter.Resolve(
            new NavigationRouteInput(inbound.Text, inbound.CallbackData, currentSection),
            labels);

        var response = await HandleRouteAsync(route, inbound, scope, localeState.Locale, cancellationToken);
        var outboundText = response.Text;

        if (localeState.IsSwitched)
        {
            var switchedText = _navigationPresenter.GetText("locale.switched", localeState.Locale);
            outboundText = string.Concat(switchedText, Environment.NewLine, Environment.NewLine, outboundText);
        }

        var sendResult = await _telegramBotSender.SendTextAsync(
            inbound.ChatId,
            outboundText,
            response.Options,
            inbound.MessageThreadId,
            cancellationToken);

        if (!sendResult.Succeeded)
        {
            _logger.LogWarning(
                "Telegram reply send failed. ChatId={ChatId}; UserId={UserId}; ConversationId={ConversationId}; Error={Error}",
                inbound.ChatId,
                scope.UserId,
                scope.ConversationId,
                sendResult.ErrorMessage);

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new TelegramWebhookResponse(Processed: true, Replied: false, Intent: response.Intent, Error: sendResult.ErrorMessage));
        }

        await _processedUpdates.MarkProcessedAsync(update.UpdateId, cancellationToken);

        _ = CleanupOldUpdatesAsync();

        return Ok(new TelegramWebhookResponse(
            Processed: true,
            Replied: true,
            Intent: response.Intent,
            Error: null));
    }

    private async Task<TelegramRouteResponse> HandleRouteAsync(
        NavigationRoute route,
        TelegramInboundMessage inbound,
        ConversationScope scope,
        string locale,
        CancellationToken cancellationToken)
    {
        switch (route.Kind)
        {
            case NavigationRouteKind.Start:
                await _navigationStateService.SetCurrentSectionAsync(scope.Channel, scope.UserId, scope.ConversationId, NavigationSections.Main, cancellationToken);
                return new TelegramRouteResponse(
                    "nav.start",
                    _navigationPresenter.GetText("start.welcome", locale),
                    MarkdownWithReplyKeyboard(_navigationPresenter.BuildMainReplyKeyboard(locale)));

            case NavigationRouteKind.MainChatButton:
                await _navigationStateService.SetCurrentSectionAsync(scope.Channel, scope.UserId, scope.ConversationId, NavigationSections.Main, cancellationToken);
                return new TelegramRouteResponse(
                    "nav.main.chat",
                    _navigationPresenter.GetText("menu.main.title", locale),
                    MarkdownWithReplyKeyboard(_navigationPresenter.BuildMainReplyKeyboard(locale)));

            case NavigationRouteKind.MainVocabularyButton:
                await _navigationStateService.SetCurrentSectionAsync(scope.Channel, scope.UserId, scope.ConversationId, NavigationSections.Vocabulary, cancellationToken);
                return await BuildVocabularySectionResponseAsync(locale, cancellationToken);

            case NavigationRouteKind.MainShoppingButton:
                await _navigationStateService.SetCurrentSectionAsync(scope.Channel, scope.UserId, scope.ConversationId, NavigationSections.Shopping, cancellationToken);
                return new TelegramRouteResponse(
                    "nav.shopping",
                    _navigationPresenter.GetText("menu.shopping.title", locale),
                    MarkdownWithInlineKeyboard(_navigationPresenter.BuildShoppingKeyboard(locale)));

            case NavigationRouteKind.MainWeeklyMenuButton:
                await _navigationStateService.SetCurrentSectionAsync(scope.Channel, scope.UserId, scope.ConversationId, NavigationSections.WeeklyMenu, cancellationToken);
                return new TelegramRouteResponse(
                    "nav.weekly",
                    _navigationPresenter.GetText("menu.weekly.title", locale),
                    MarkdownWithInlineKeyboard(_navigationPresenter.BuildWeeklyMenuKeyboard(locale)));

            case NavigationRouteKind.Callback:
                return await HandleCallbackAsync(route.CallbackData!, scope, locale, cancellationToken);

            case NavigationRouteKind.VocabularyText:
                {
                    var result = await _orchestrator.ProcessAsync(
                        inbound.Text,
                        scope.Channel,
                        scope.UserId,
                        scope.ConversationId,
                        cancellationToken);
                    return new TelegramRouteResponse(result.Intent, _responseFormatter.Format(result));
                }

            case NavigationRouteKind.ShoppingText:
                return new TelegramRouteResponse(
                    "shopping.stub",
                    _navigationPresenter.GetText("stub.wip", locale),
                    MarkdownWithInlineKeyboard(_navigationPresenter.BuildShoppingKeyboard(locale)));

            case NavigationRouteKind.WeeklyMenuText:
                return new TelegramRouteResponse(
                    "weekly.stub",
                    _navigationPresenter.GetText("stub.wip", locale),
                    MarkdownWithInlineKeyboard(_navigationPresenter.BuildWeeklyMenuKeyboard(locale)));

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
        CancellationToken cancellationToken)
    {
        if (string.Equals(callbackData, "nav:main", StringComparison.Ordinal))
        {
            await _navigationStateService.SetCurrentSectionAsync(scope.Channel, scope.UserId, scope.ConversationId, NavigationSections.Main, cancellationToken);
            return new TelegramRouteResponse(
                "nav.main",
                _navigationPresenter.GetText("menu.main.title", locale),
                MarkdownWithReplyKeyboard(_navigationPresenter.BuildMainReplyKeyboard(locale)));
        }

        if (callbackData.StartsWith("vocab:", StringComparison.Ordinal))
        {
            await _navigationStateService.SetCurrentSectionAsync(scope.Channel, scope.UserId, scope.ConversationId, NavigationSections.Vocabulary, cancellationToken);

            return callbackData switch
            {
                "vocab:add" => new TelegramRouteResponse(
                    "vocab.add",
                    _navigationPresenter.GetText("vocab.add.prompt", locale),
                    MarkdownWithInlineKeyboard(_navigationPresenter.BuildVocabularyKeyboard(locale))),
                "vocab:list" => await BuildVocabularyListResponseAsync(locale, cancellationToken),
                "vocab:url" => new TelegramRouteResponse(
                    "vocab.url",
                    _navigationPresenter.GetText("vocab.url.prompt", locale),
                    MarkdownWithInlineKeyboard(_navigationPresenter.BuildVocabularyKeyboard(locale))),
                "vocab:batch" => await BuildBatchModeResponseAsync(scope, locale, cancellationToken),
                _ => new TelegramRouteResponse(
                    "vocab.unknown",
                    _navigationPresenter.GetText("stub.wip", locale),
                    MarkdownWithInlineKeyboard(_navigationPresenter.BuildVocabularyKeyboard(locale)))
            };
        }

        if (callbackData.StartsWith("shop:", StringComparison.Ordinal))
        {
            await _navigationStateService.SetCurrentSectionAsync(scope.Channel, scope.UserId, scope.ConversationId, NavigationSections.Shopping, cancellationToken);
            return new TelegramRouteResponse(
                "shopping.stub",
                _navigationPresenter.GetText("stub.wip", locale),
                MarkdownWithInlineKeyboard(_navigationPresenter.BuildShoppingKeyboard(locale)));
        }

        if (callbackData.StartsWith("weekly:", StringComparison.Ordinal))
        {
            await _navigationStateService.SetCurrentSectionAsync(scope.Channel, scope.UserId, scope.ConversationId, NavigationSections.WeeklyMenu, cancellationToken);
            return new TelegramRouteResponse(
                "weekly.stub",
                _navigationPresenter.GetText("stub.wip", locale),
                MarkdownWithInlineKeyboard(_navigationPresenter.BuildWeeklyMenuKeyboard(locale)));
        }

        return new TelegramRouteResponse("nav.unknown", _navigationPresenter.GetText("stub.wip", locale));
    }

    private async Task<TelegramRouteResponse> BuildVocabularySectionResponseAsync(string locale, CancellationToken cancellationToken)
    {
        var count = await _vocabularyCardRepository.CountAllAsync(cancellationToken);
        var title = _navigationPresenter.GetText("menu.vocabulary.title", locale, count);

        return new TelegramRouteResponse(
            "vocab.section",
            title,
            MarkdownWithInlineKeyboard(_navigationPresenter.BuildVocabularyKeyboard(locale)));
    }

    private async Task<TelegramRouteResponse> BuildVocabularyListResponseAsync(string locale, CancellationToken cancellationToken)
    {
        var recent = await _vocabularyCardRepository.GetRecentAsync(10, cancellationToken);
        if (recent.Count == 0)
        {
            return new TelegramRouteResponse(
                "vocab.list",
                _navigationPresenter.GetText("vocab.list.empty", locale),
                MarkdownWithInlineKeyboard(_navigationPresenter.BuildVocabularyKeyboard(locale)));
        }

        var lines = new List<string>
        {
            _navigationPresenter.GetText("vocab.list.title", locale)
        };

        for (var i = 0; i < recent.Count; i++)
        {
            var pos = string.IsNullOrWhiteSpace(recent[i].PartOfSpeechMarker)
                ? string.Empty
                : $" ({recent[i].PartOfSpeechMarker})";
            lines.Add($"{i + 1}) {recent[i].Word}{pos}");
        }

        return new TelegramRouteResponse(
            "vocab.list",
            string.Join(Environment.NewLine, lines),
            MarkdownWithInlineKeyboard(_navigationPresenter.BuildVocabularyKeyboard(locale)));
    }

    private async Task<TelegramRouteResponse> BuildBatchModeResponseAsync(
        ConversationScope scope,
        string locale,
        CancellationToken cancellationToken)
    {
        var result = await _orchestrator.ProcessAsync(
            "/batch",
            scope.Channel,
            scope.UserId,
            scope.ConversationId,
            cancellationToken);

        var text = _responseFormatter.Format(result);
        return new TelegramRouteResponse(
            "vocab.batch",
            text,
            MarkdownWithInlineKeyboard(_navigationPresenter.BuildVocabularyKeyboard(locale)));
    }

    private static TelegramSendOptions MarkdownWithReplyKeyboard(TelegramReplyKeyboardMarkup keyboard)
        => new(ParseMode: "Markdown", ReplyMarkup: keyboard);

    private static TelegramSendOptions MarkdownWithInlineKeyboard(TelegramInlineKeyboardMarkup keyboard)
        => new(ParseMode: "Markdown", ReplyMarkup: keyboard);

    private async Task CleanupOldUpdatesAsync()
    {
        try
        {
            var cutoff = DateTimeOffset.UtcNow.AddHours(-25);
            await _processedUpdates.DeleteOlderThanAsync(cutoff);
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

    private sealed record TelegramRouteResponse(
        string Intent,
        string Text,
        TelegramSendOptions? Options = null);
}
