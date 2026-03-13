namespace LagerthaAssistant.Api.Contracts;

public sealed record NotionSyncFailedCardResponse(
    long CardId,
    string Word,
    string DeckFileName,
    string StorageMode,
    int AttemptCount,
    string? LastError,
    DateTimeOffset? LastAttemptAtUtc,
    DateTimeOffset LastSeenAtUtc);

