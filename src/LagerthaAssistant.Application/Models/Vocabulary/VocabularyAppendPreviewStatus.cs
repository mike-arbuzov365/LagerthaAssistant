namespace LagerthaAssistant.Application.Models.Vocabulary;

public enum VocabularyAppendPreviewStatus
{
    ReadyToAppend,
    DuplicateFound,
    ParseFailed,
    NoWritableDecks,
    NoMatchingDeck,
    Error
}