namespace LagerthaAssistant.Api.Contracts;

public sealed record VocabularySyncFailedJobResponse(
    int Id,
    string RequestedWord,
    string TargetDeckFileName,
    string StorageMode,
    int AttemptCount,
    string? LastError,
    DateTimeOffset? LastAttemptAtUtc,
    DateTimeOffset CreatedAtUtc);
