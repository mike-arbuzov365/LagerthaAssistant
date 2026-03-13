namespace LagerthaAssistant.Application.Interfaces.Vocabulary;

using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Models.Vocabulary;

public interface IVocabularyStoragePreferenceService
{
    IReadOnlyList<string> SupportedModes { get; }

    Task<VocabularyStorageMode> GetModeAsync(ConversationScope scope, CancellationToken cancellationToken = default);

    Task<VocabularyStorageMode> SetModeAsync(
        ConversationScope scope,
        VocabularyStorageMode mode,
        CancellationToken cancellationToken = default);
}
