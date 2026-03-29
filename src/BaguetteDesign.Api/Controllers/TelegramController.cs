namespace BaguetteDesign.Api.Controllers;

using System.Text.Json;
using System.Text.Json.Serialization;
using BaguetteDesign.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/telegram")]
public sealed class TelegramController : ControllerBase
{
    private readonly IStartCommandHandler _startHandler;

    public TelegramController(IStartCommandHandler startHandler)
    {
        _startHandler = startHandler;
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook(
        [FromBody] TelegramUpdate update,
        CancellationToken cancellationToken)
    {
        var message = update.Message;
        if (message is null)
            return Ok();

        var chatId = message.Chat.Id;
        var userId = message.From?.Id ?? chatId;
        var text = message.Text?.Trim() ?? string.Empty;
        var languageCode = message.From?.LanguageCode;

        if (text.StartsWith("/start", StringComparison.OrdinalIgnoreCase))
        {
            await _startHandler.HandleAsync(chatId, userId, languageCode, cancellationToken);
        }

        return Ok();
    }
}

public sealed record TelegramUpdate(
    [property: JsonPropertyName("update_id")] long UpdateId,
    [property: JsonPropertyName("message")] TelegramMessage? Message);

public sealed record TelegramMessage(
    [property: JsonPropertyName("message_id")] long MessageId,
    [property: JsonPropertyName("from")] TelegramUser? From,
    [property: JsonPropertyName("chat")] TelegramChat Chat,
    [property: JsonPropertyName("text")] string? Text);

public sealed record TelegramUser(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("language_code")] string? LanguageCode);

public sealed record TelegramChat(
    [property: JsonPropertyName("id")] long Id);
