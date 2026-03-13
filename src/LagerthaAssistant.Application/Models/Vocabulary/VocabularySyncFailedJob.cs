namespace LagerthaAssistant.Application.Models.Vocabulary;

public sealed record VocabularySyncFailedJob(
    int Id,
    string RequestedWord,
    string TargetDeckFileName,
    string StorageMode,
    int AttemptCount,
    string? LastError,
    DateTimeOffset? LastAttemptAtUtc,
    DateTimeOffset CreatedAtUtc);
