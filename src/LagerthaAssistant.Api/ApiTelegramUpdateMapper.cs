using System.Globalization;
using LagerthaAssistant.Api.Contracts;

namespace LagerthaAssistant.Api;

internal static class ApiTelegramUpdateMapper
{
    public static bool TryMapInbound(
        TelegramWebhookUpdateRequest update,
        out TelegramInboundMessage inbound)
    {
        inbound = default;

        if (TryMapCallback(update, out inbound))
        {
            return true;
        }

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
            MessageThreadId: message.MessageThreadId,
            LanguageCode: message.From?.LanguageCode,
            CallbackData: null,
            IsCallback: false);
        return true;
    }

    private static bool TryMapCallback(
        TelegramWebhookUpdateRequest update,
        out TelegramInboundMessage inbound)
    {
        inbound = default;

        var callback = update.CallbackQuery;
        var message = callback?.Message;
        if (callback is null || message?.Chat is null || string.IsNullOrWhiteSpace(callback.Data))
        {
            return false;
        }

        var chatId = message.Chat.Id;
        var userId = callback.From?.Id ?? chatId;
        var conversationId = message.MessageThreadId.HasValue
            ? $"{chatId.ToString(CultureInfo.InvariantCulture)}:{message.MessageThreadId.Value.ToString(CultureInfo.InvariantCulture)}"
            : chatId.ToString(CultureInfo.InvariantCulture);

        inbound = new TelegramInboundMessage(
            ChatId: chatId,
            UserId: userId.ToString(CultureInfo.InvariantCulture),
            ConversationId: conversationId,
            Text: string.Empty,
            MessageThreadId: message.MessageThreadId,
            LanguageCode: callback.From?.LanguageCode,
            CallbackData: callback.Data.Trim(),
            IsCallback: true);
        return true;
    }
}

internal readonly record struct TelegramInboundMessage(
    long ChatId,
    string UserId,
    string ConversationId,
    string Text,
    int? MessageThreadId,
    string? LanguageCode,
    string? CallbackData,
    bool IsCallback);
