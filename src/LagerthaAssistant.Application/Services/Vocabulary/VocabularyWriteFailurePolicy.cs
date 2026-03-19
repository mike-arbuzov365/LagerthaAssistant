namespace LagerthaAssistant.Application.Services.Vocabulary;

internal enum VocabularyWriteFailureCategory
{
    Unknown = 0,
    Recoverable = 1,
    NonRecoverable = 2
}

internal static class VocabularyWriteFailurePolicy
{
    private static readonly string[] RecoverableMarkers =
    [
        "graph authentication is required",
        "not authenticated",
        "authorization is required",
        "authorization failed",
        "run /graph login",
        "use /graph login",
        "could not resolve onedrive target deck",
        "not writable or was not found",
        "required deck files are missing",
        "open in another app",
        "currently in use",
        "file is locked",
        "locked right now",
        "version conflict",
        "temporarily unavailable",
        "service unavailable",
        "timeout",
        "timed out",
        "connection reset",
        "connection refused",
        "network",
        "too many requests",
        "throttl",
        " 429"
    ];

    private static readonly string[] NonRecoverableMarkers =
    [
        "parse failed",
        "assistant response format is invalid",
        "word is empty after parsing",
        "no writable vocabulary decks",
        "is not a writable deck",
        "unknown storage mode"
    ];

    public static VocabularyWriteFailureCategory Classify(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return VocabularyWriteFailureCategory.Unknown;
        }

        foreach (var marker in NonRecoverableMarkers)
        {
            if (message.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                return VocabularyWriteFailureCategory.NonRecoverable;
            }
        }

        foreach (var marker in RecoverableMarkers)
        {
            if (message.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                return VocabularyWriteFailureCategory.Recoverable;
            }
        }

        return VocabularyWriteFailureCategory.Unknown;
    }

    public static bool ShouldQueueAfterInitialAppendError(string? message)
        => Classify(message) == VocabularyWriteFailureCategory.Recoverable;

    public static bool ShouldRequeueQueuedJob(string? message, int attemptCount, int maxRecoverableAttempts)
    {
        if (attemptCount >= maxRecoverableAttempts)
        {
            return false;
        }

        return Classify(message) != VocabularyWriteFailureCategory.NonRecoverable;
    }
}
