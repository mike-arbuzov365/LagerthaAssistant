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
    [property: JsonPropertyName("message_thread_id")] int? MessageThreadId);

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

public sealed record TelegramWebhookResponse(
    bool Processed,
    bool Replied,
    string? Intent,
    string? Error);
