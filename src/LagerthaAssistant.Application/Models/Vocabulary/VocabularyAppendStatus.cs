namespace LagerthaAssistant.Application.Models.Vocabulary;

public enum VocabularyAppendStatus
{
    Added,
    DuplicateFound,
    ParseFailed,
    NoWritableDecks,
    NoMatchingDeck,
    Error
}
