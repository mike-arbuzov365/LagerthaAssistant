namespace LagerthaAssistant.Application.Interfaces.Vocabulary;

using LagerthaAssistant.Application.Models.Vocabulary;

public interface IVocabularyStorageModeProvider
{
    VocabularyStorageMode CurrentMode { get; }

    void SetMode(VocabularyStorageMode mode);

    bool TryParse(string? value, out VocabularyStorageMode mode);

    string ToText(VocabularyStorageMode mode);
}
