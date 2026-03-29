namespace LagerthaAssistant.Domain.Entities;

using LagerthaAssistant.Domain.Enums;

public sealed class VocabularySyncJob : AuditableEntity
{
    public string RequestedWord { get; set; } = string.Empty;

    public string AssistantReply { get; set; } = string.Empty;

    public string TargetDeckFileName { get; set; } = string.Empty;

    public string? TargetDeckPath { get; set; }

    public string? OverridePartOfSpeech { get; set; }

    public string StorageMode { get; set; } = string.Empty;

    public VocabularySyncJobStatus Status { get; set; } = VocabularySyncJobStatus.Pending;

    public int AttemptCount { get; set; }

    public string? LastError { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? LastAttemptAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }
}
