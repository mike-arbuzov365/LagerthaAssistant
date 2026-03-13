namespace LagerthaAssistant.Api.Contracts;

public sealed record ConversationMemoryEntryResponse(
    string Key,
    string Value,
    double Confidence,
    bool IsActive,
    DateTimeOffset LastSeenAtUtc);

