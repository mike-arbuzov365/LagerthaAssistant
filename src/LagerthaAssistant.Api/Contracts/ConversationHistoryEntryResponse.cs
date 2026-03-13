namespace LagerthaAssistant.Api.Contracts;

public sealed record ConversationHistoryEntryResponse(
    string Role,
    string Content,
    DateTimeOffset SentAtUtc);

