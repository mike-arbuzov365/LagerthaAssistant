using System.Globalization;
using LagerthaAssistant.Api.Contracts;

namespace LagerthaAssistant.Api;

internal static class ApiTelegramUpdateMapper
{
    public static bool TryMapTextMessage(
        TelegramWebhookUpdateRequest update,
        out TelegramInboundMessage inbound)
    {
        inbound = default;

        var message = update.Message ?? update.EditedMessage;
        if (message is null || message.Chat is null)
        {
            return false;
        }

        var text = message.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            text = message.Caption;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var chatId = message.Chat.Id;
        var userId = message.From?.Id ?? chatId;
        var conversationId = message.MessageThreadId.HasValue
            ? $"{chatId.ToString(CultureInfo.InvariantCulture)}:{message.MessageThreadId.Value.ToString(CultureInfo.InvariantCulture)}"
            : chatId.ToString(CultureInfo.InvariantCulture);

        inbound = new TelegramInboundMessage(
            ChatId: chatId,
            UserId: userId.ToString(CultureInfo.InvariantCulture),
            ConversationId: conversationId,
            Text: text.Trim(),
            MessageThreadId: message.MessageThreadId);
        return true;
    }
}

internal readonly record struct TelegramInboundMessage(
    long ChatId,
    string UserId,
    string ConversationId,
    string Text,
    int? MessageThreadId);
