namespace LagerthaAssistant.Application.Models.Vocabulary;

public sealed record VocabularySessionPreferences(
    VocabularySaveMode SaveMode,
    VocabularyStorageMode StorageMode);
