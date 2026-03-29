namespace LagerthaAssistant.Domain.Entities;

using LagerthaAssistant.Domain.Enums;

public sealed class VocabularyCard : AuditableEntity
{
    public string Word { get; set; } = string.Empty;

    public string NormalizedWord { get; set; } = string.Empty;

    public string Meaning { get; set; } = string.Empty;

    public string Examples { get; set; } = string.Empty;

    public string? PartOfSpeechMarker { get; set; }

    public string DeckFileName { get; set; } = string.Empty;

    public string DeckPath { get; set; } = string.Empty;

    public int LastKnownRowNumber { get; set; }

    public string StorageMode { get; set; } = string.Empty;

    public VocabularySyncStatus SyncStatus { get; set; } = VocabularySyncStatus.Synced;

    public string? LastSyncError { get; set; }

    public DateTimeOffset FirstSeenAtUtc { get; set; }

    public DateTimeOffset LastSeenAtUtc { get; set; }

    public DateTimeOffset? SyncedAtUtc { get; set; }

    public NotionSyncStatus NotionSyncStatus { get; set; } = NotionSyncStatus.Pending;

    public string? NotionPageId { get; set; }

    public int NotionAttemptCount { get; set; }

    public string? NotionLastError { get; set; }

    public DateTimeOffset? NotionLastAttemptAtUtc { get; set; }

    public DateTimeOffset? NotionSyncedAtUtc { get; set; }

    public ICollection<VocabularyCardToken> Tokens { get; set; } = [];
}
