using System.Security.Cryptography;
using System.Text;
using LagerthaAssistant.Api.Contracts;
using LagerthaAssistant.Api.Interfaces;
using LagerthaAssistant.Api.Options;
using LagerthaAssistant.Application.Interfaces.Agents;
using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Interfaces.Repositories;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
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
    private readonly IConversationScopeAccessor _scopeAccessor;
    private readonly IVocabularyStorageModeProvider _storageModeProvider;
    private readonly IVocabularyStoragePreferenceService _storagePreferenceService;
    private readonly ITelegramConversationResponseFormatter _responseFormatter;
    private readonly ITelegramBotSender _telegramBotSender;
    private readonly ITelegramProcessedUpdateRepository _processedUpdates;
    private readonly TelegramOptions _options;
    private readonly ILogger<TelegramController> _logger;

    public TelegramController(
        IConversationOrchestrator orchestrator,
        IConversationScopeAccessor scopeAccessor,
        IVocabularyStorageModeProvider storageModeProvider,
        IVocabularyStoragePreferenceService storagePreferenceService,
        ITelegramConversationResponseFormatter responseFormatter,
        ITelegramBotSender telegramBotSender,
        ITelegramProcessedUpdateRepository processedUpdates,
        IOptions<TelegramOptions> options,
        ILogger<TelegramController> logger)
    {
        _orchestrator = orchestrator;
        _scopeAccessor = scopeAccessor;
        _storageModeProvider = storageModeProvider;
        _storagePreferenceService = storagePreferenceService;
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

        if (update is null || !ApiTelegramUpdateMapper.TryMapTextMessage(update, out var inbound))
        {
            return Ok(new TelegramWebhookResponse(false, false, null, "Ignored: no text message."));
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

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new TelegramWebhookResponse(Processed: true, Replied: false, Intent: result.Intent, Error: sendResult.ErrorMessage));
        }

        await _processedUpdates.MarkProcessedAsync(update.UpdateId, cancellationToken);

        _ = CleanupOldUpdatesAsync();

        return Ok(new TelegramWebhookResponse(
            Processed: true,
            Replied: true,
            Intent: result.Intent,
            Error: null));
    }

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
}
