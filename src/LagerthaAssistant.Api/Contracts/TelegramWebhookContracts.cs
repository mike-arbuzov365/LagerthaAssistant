using System.Text.Json.Serialization;

namespace LagerthaAssistant.Api.Contracts;

public sealed record TelegramWebhookUpdateRequest(
    [property: JsonPropertyName("update_id")] long UpdateId,
    [property: JsonPropertyName("message")] TelegramIncomingMessage? Message,
    [property: JsonPropertyName("edited_message")] TelegramIncomingMessage? EditedMessage,
    [property: JsonPropertyName("callback_query")] TelegramCallbackQuery? CallbackQuery);

public sealed record TelegramIncomingMessage(
    [property: JsonPropertyName("message_id")] long MessageId,
    [property: JsonPropertyName("from")] TelegramUserInfo? From,
    [property: JsonPropertyName("chat")] TelegramChatInfo Chat,
    [property: JsonPropertyName("text")] string? Text,
    [property: JsonPropertyName("caption")] string? Caption,
    [property: JsonPropertyName("message_thread_id")] int? MessageThreadId,
    [property: JsonPropertyName("document")] TelegramIncomingDocument? Document = null,
    [property: JsonPropertyName("photo")] IReadOnlyList<TelegramIncomingPhotoSize>? Photo = null);

public sealed record TelegramUserInfo(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("is_bot")] bool IsBot,
    [property: JsonPropertyName("language_code")] string? LanguageCode,
    [property: JsonPropertyName("username")] string? Username,
    [property: JsonPropertyName("first_name")] string? FirstName,
    [property: JsonPropertyName("last_name")] string? LastName);

public sealed record TelegramChatInfo(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("username")] string? Username,
    [property: JsonPropertyName("title")] string? Title);

public sealed record TelegramCallbackQuery(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("from")] TelegramUserInfo? From,
    [property: JsonPropertyName("message")] TelegramIncomingMessage? Message,
    [property: JsonPropertyName("data")] string? Data);

public sealed record TelegramIncomingDocument(
    [property: JsonPropertyName("file_id")] string FileId,
    [property: JsonPropertyName("file_unique_id")] string? FileUniqueId,
    [property: JsonPropertyName("file_name")] string? FileName,
    [property: JsonPropertyName("mime_type")] string? MimeType,
    [property: JsonPropertyName("file_size")] int? FileSize);

public sealed record TelegramIncomingPhotoSize(
    [property: JsonPropertyName("file_id")] string FileId,
    [property: JsonPropertyName("file_unique_id")] string? FileUniqueId,
    [property: JsonPropertyName("width")] int Width,
    [property: JsonPropertyName("height")] int Height,
    [property: JsonPropertyName("file_size")] int? FileSize);

public sealed record TelegramWebhookResponse(
    bool Processed,
    bool Replied,
    string? Intent,
    string? Error);
