namespace LagerthaAssistant.Api.Contracts;

public sealed record ConversationMessageRequest(
    string Input,
    string? Channel = null,
    string? UserId = null,
    string? ConversationId = null,
    string? StorageMode = null);
