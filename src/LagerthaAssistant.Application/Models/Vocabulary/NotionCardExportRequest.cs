namespace LagerthaAssistant.Application.Models.Vocabulary;

public sealed record NotionCardExportRequest(
    long CardId,
    string IdentityKey,
    string Word,
    string Meaning,
    string Examples,
    string? PartOfSpeechMarker,
    string DeckFileName,
    string StorageMode,
    int RowNumber,
    DateTimeOffset LastSeenAtUtc,
    string? ExistingPageId);

