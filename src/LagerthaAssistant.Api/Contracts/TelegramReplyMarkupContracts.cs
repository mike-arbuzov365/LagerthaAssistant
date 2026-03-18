using System.Text.Json.Serialization;

namespace LagerthaAssistant.Api.Contracts;

public sealed record TelegramReplyKeyboardMarkup(
    [property: JsonPropertyName("keyboard")] IReadOnlyList<IReadOnlyList<TelegramKeyboardButton>> Keyboard,
    [property: JsonPropertyName("resize_keyboard")] bool ResizeKeyboard = true,
    [property: JsonPropertyName("is_persistent")] bool IsPersistent = true);

public sealed record TelegramKeyboardButton(
    [property: JsonPropertyName("text")] string Text);

public sealed record TelegramInlineKeyboardMarkup(
    [property: JsonPropertyName("inline_keyboard")] IReadOnlyList<IReadOnlyList<TelegramInlineKeyboardButton>> InlineKeyboard);

public sealed record TelegramInlineKeyboardButton(
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("callback_data")] string CallbackData);
