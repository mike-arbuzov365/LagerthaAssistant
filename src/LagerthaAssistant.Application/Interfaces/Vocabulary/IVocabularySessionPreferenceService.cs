namespace LagerthaAssistant.Application.Interfaces.Vocabulary;

using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Models.Vocabulary;

public interface IVocabularySessionPreferenceService
{
    IReadOnlyList<string> SupportedSaveModes { get; }

    IReadOnlyList<string> SupportedStorageModes { get; }

    Task<VocabularySessionPreferences> GetAsync(
        ConversationScope scope,
        CancellationToken cancellationToken = default);

    Task<VocabularySessionPreferences> SetAsync(
        ConversationScope scope,
        VocabularySaveMode? saveMode = null,
        VocabularyStorageMode? storageMode = null,
        CancellationToken cancellationToken = default);
}
