namespace LagerthaAssistant.Application.Models.Vocabulary;

public sealed record ParsedVocabularyReply(
    string Word,
    IReadOnlyList<string> Meanings,
    IReadOnlyList<string> Examples,
    IReadOnlyList<string> PartsOfSpeech);
