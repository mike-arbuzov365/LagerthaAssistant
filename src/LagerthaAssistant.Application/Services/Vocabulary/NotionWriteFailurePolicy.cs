namespace LagerthaAssistant.Application.Services.Vocabulary;

internal enum NotionWriteFailureCategory
{
    Unknown = 0,
    Recoverable = 1,
    NonRecoverable = 2
}

internal static class NotionWriteFailurePolicy
{
    private static readonly string[] RecoverableMarkers =
    [
        "timeout",
        "timed out",
        "temporarily unavailable",
        "service unavailable",
        "too many requests",
        "throttl",
        " 429",
        "network",
        "connection reset",
        "connection refused",
        "http 5"
    ];

    private static readonly string[] NonRecoverableMarkers =
    [
        "notion integration is disabled",
        "notion is not configured",
        "database id",
        "api key",
        "unauthorized",
        "forbidden",
        "bad request",
        "validation_error",
        "conflict rule set to 'error'"
    ];

    public static NotionWriteFailureCategory Classify(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return NotionWriteFailureCategory.Unknown;
        }

        foreach (var marker in NonRecoverableMarkers)
        {
            if (message.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                return NotionWriteFailureCategory.NonRecoverable;
            }
        }

        foreach (var marker in RecoverableMarkers)
        {
            if (message.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                return NotionWriteFailureCategory.Recoverable;
            }
        }

        return NotionWriteFailureCategory.Unknown;
    }

    public static bool ShouldRequeue(string? message, int attemptCount, int maxRecoverableAttempts)
    {
        if (attemptCount >= maxRecoverableAttempts)
        {
            return false;
        }

        return Classify(message) != NotionWriteFailureCategory.NonRecoverable;
    }
}

