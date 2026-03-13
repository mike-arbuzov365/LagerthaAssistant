namespace LagerthaAssistant.Api.Contracts;

public sealed record ConversationSystemPromptHistoryEntryResponse(
    int Version,
    string PromptText,
    string Source,
    bool IsActive,
    DateTimeOffset CreatedAtUtc);

