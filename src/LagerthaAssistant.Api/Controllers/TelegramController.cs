using LagerthaAssistant.Api.Contracts;
using LagerthaAssistant.Api.Interfaces;
using LagerthaAssistant.Api.Options;
using LagerthaAssistant.Application.Interfaces.Agents;
using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LagerthaAssistant.Api.Controllers;

[ApiController]
[Route("api/telegram")]
public sealed class TelegramController : ControllerBase
{
    private const string TelegramChannel = "telegram";
    private const string TelegramSecretHeader = "X-Telegram-Bot-Api-Secret-Token";

    private readonly IConversationOrchestrator _orchestrator;
    private readonly IConversationScopeAccessor _scopeAccessor;
    private readonly IVocabularyStorageModeProvider _storageModeProvider;
    private readonly IVocabularyStoragePreferenceService _storagePreferenceService;
    private readonly ITelegramConversationResponseFormatter _responseFormatter;
    private readonly ITelegramBotSender _telegramBotSender;
    private readonly TelegramOptions _options;
    private readonly ILogger<TelegramController> _logger;

    public TelegramController(
        IConversationOrchestrator orchestrator,
        IConversationScopeAccessor scopeAccessor,
        IVocabularyStorageModeProvider storageModeProvider,
        IVocabularyStoragePreferenceService storagePreferenceService,
        ITelegramConversationResponseFormatter responseFormatter,
        ITelegramBotSender telegramBotSender,
        IOptions<TelegramOptions> options,
        ILogger<TelegramController> logger)
    {
        _orchestrator = orchestrator;
        _scopeAccessor = scopeAccessor;
        _storageModeProvider = storageModeProvider;
        _storagePreferenceService = storagePreferenceService;
        _responseFormatter = responseFormatter;
        _telegramBotSender = telegramBotSender;
        _options = options.Value;
        _logger = logger;
    }

    [HttpPost("webhook")]
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

        if (!_options.Enabled)
        {
            return Ok(new TelegramWebhookResponse(false, false, null, "Telegram integration is disabled."));
        }

        if (update is null || !ApiTelegramUpdateMapper.TryMapTextMessage(update, out var inbound))
        {
            return Ok(new TelegramWebhookResponse(false, false, null, "Ignored: no text message."));
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

        var result = await _orchestrator.ProcessAsync(
            inbound.Text,
            scope.Channel,
            scope.UserId,
            scope.ConversationId,
            cancellationToken);

        var outboundText = _responseFormatter.Format(result);
        var sendResult = await _telegramBotSender.SendTextAsync(
            inbound.ChatId,
            outboundText,
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
        }

        return Ok(new TelegramWebhookResponse(
            Processed: true,
            Replied: sendResult.Succeeded,
            Intent: result.Intent,
            Error: sendResult.Succeeded ? null : sendResult.ErrorMessage));
    }

    private bool IsSecretValid()
    {
        if (string.IsNullOrWhiteSpace(_options.WebhookSecret))
        {
            return true;
        }

        var received = Request.Headers[TelegramSecretHeader].FirstOrDefault();
        return string.Equals(received, _options.WebhookSecret, StringComparison.Ordinal);
    }
}
