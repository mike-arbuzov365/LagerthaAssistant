namespace LagerthaAssistant.Api.Interfaces;

public interface ITelegramBotSender
{
    Task<TelegramSendResult> SendTextAsync(
        long chatId,
        string text,
        TelegramSendOptions? options = null,
        int? messageThreadId = null,
        CancellationToken cancellationToken = default);
}

public sealed record TelegramSendResult(
    bool Succeeded,
    string? ErrorMessage = null,
    int? HttpStatusCode = null);

public sealed record TelegramSendOptions(
    string? ParseMode = null,
    object? ReplyMarkup = null);
