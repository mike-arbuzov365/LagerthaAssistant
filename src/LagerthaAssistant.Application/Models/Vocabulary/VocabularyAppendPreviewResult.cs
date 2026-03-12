namespace LagerthaAssistant.Application.Models.Vocabulary;

public sealed record VocabularyAppendPreviewResult(
    VocabularyAppendPreviewStatus Status,
    string Word,
    string? TargetDeckFileName = null,
    string? TargetDeckPath = null,
    IReadOnlyList<VocabularyDeckEntry>? DuplicateMatches = null,
    string? Message = null);