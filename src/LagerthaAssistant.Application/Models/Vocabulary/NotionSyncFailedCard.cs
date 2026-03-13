namespace LagerthaAssistant.Application.Models.Vocabulary;

public sealed record NotionSyncFailedCard(
    long CardId,
    string Word,
    string DeckFileName,
    string StorageMode,
    int AttemptCount,
    string? LastError,
    DateTimeOffset? LastAttemptAtUtc,
    DateTimeOffset LastSeenAtUtc);

