namespace LagerthaAssistant.Application.Models.Vocabulary;

public sealed record VocabularyAppendResult(
    VocabularyAppendStatus Status,
    VocabularyDeckEntry? Entry = null,
    IReadOnlyList<VocabularyDeckEntry>? DuplicateMatches = null,
    string? Message = null);
