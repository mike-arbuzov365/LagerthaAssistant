namespace LagerthaAssistant.Application.Models.Vocabulary;

public sealed record VocabularyDeckEntry(
    string DeckFileName,
    string DeckPath,
    int RowNumber,
    string Word,
    string Meaning,
    string Examples);
