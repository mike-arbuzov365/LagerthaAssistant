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

        var webAppData = message.WebAppData?.Data?.Trim();

        var photoFileId = ResolveLargestPhotoFileId(message);
        var documentFileId = message.Document?.FileId?.Trim();
        var hasMedia = !string.IsNullOrWhiteSpace(photoFileId) || !string.IsNullOrWhiteSpace(documentFileId);

        if (string.IsNullOrWhiteSpace(text) && !hasMedia && string.IsNullOrWhiteSpace(webAppData))
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
            Text: text?.Trim() ?? string.Empty,
            MessageThreadId: message.MessageThreadId,
            LanguageCode: message.From?.LanguageCode,
            CallbackData: null,
            CallbackQueryId: null,
            DocumentFileId: documentFileId,
            DocumentFileName: message.Document?.FileName,
            DocumentMimeType: message.Document?.MimeType,
            PhotoFileId: photoFileId,
            WebAppData: string.IsNullOrWhiteSpace(webAppData) ? null : webAppData,
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
            CallbackQueryId: callback.Id,
            DocumentFileId: null,
            DocumentFileName: null,
            DocumentMimeType: null,
            PhotoFileId: null,
            WebAppData: null,
            IsCallback: true);
        return true;
    }

    private static string? ResolveLargestPhotoFileId(TelegramIncomingMessage message)
    {
        var photos = message.Photo;
        if (photos is null || photos.Count == 0)
        {
            return null;
        }

        return photos
            .OrderByDescending(photo => photo.FileSize ?? 0)
            .Select(photo => photo.FileId?.Trim())
            .FirstOrDefault(fileId => !string.IsNullOrWhiteSpace(fileId));
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
    string? CallbackQueryId,
    string? DocumentFileId,
    string? DocumentFileName,
    string? DocumentMimeType,
    string? PhotoFileId,
    string? WebAppData,
    bool IsCallback);
