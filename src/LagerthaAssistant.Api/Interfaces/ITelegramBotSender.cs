namespace LagerthaAssistant.Api.Interfaces;

public interface ITelegramBotSender
{
    Task<TelegramSendResult> SendTextAsync(
        long chatId,
        string text,
        int? messageThreadId = null,
        CancellationToken cancellationToken = default);
}

public sealed record TelegramSendResult(
    bool Succeeded,
    string? ErrorMessage = null,
    int? HttpStatusCode = null);
