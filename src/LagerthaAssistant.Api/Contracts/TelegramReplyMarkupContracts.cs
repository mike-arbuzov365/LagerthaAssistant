using System.Text.Json.Serialization;

namespace LagerthaAssistant.Api.Contracts;

public sealed record TelegramReplyKeyboardMarkup(
    [property: JsonPropertyName("keyboard")] IReadOnlyList<IReadOnlyList<TelegramKeyboardButton>> Keyboard,
    [property: JsonPropertyName("resize_keyboard")] bool ResizeKeyboard = true,
    [property: JsonPropertyName("is_persistent")] bool IsPersistent = true);

public sealed record TelegramKeyboardButton(
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("web_app"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    TelegramWebAppInfo? WebApp = null);

public sealed record TelegramWebAppInfo(
    [property: JsonPropertyName("url")] string Url);

public sealed record TelegramInlineKeyboardMarkup(
    [property: JsonPropertyName("inline_keyboard")] IReadOnlyList<IReadOnlyList<TelegramInlineKeyboardButton>> InlineKeyboard);

public sealed record TelegramInlineKeyboardButton(
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("callback_data"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? CallbackData = null,
    [property: JsonPropertyName("url"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Url = null,
    [property: JsonPropertyName("web_app"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    TelegramWebAppInfo? WebApp = null);
