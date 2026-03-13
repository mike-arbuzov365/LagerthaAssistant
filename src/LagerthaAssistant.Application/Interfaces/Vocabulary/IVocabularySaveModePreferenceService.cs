namespace LagerthaAssistant.Application.Interfaces.Vocabulary;

using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Models.Vocabulary;

public interface IVocabularySaveModePreferenceService
{
    IReadOnlyList<string> SupportedModes { get; }

    bool TryParse(string? value, out VocabularySaveMode mode);

    string ToText(VocabularySaveMode mode);

    Task<VocabularySaveMode> GetModeAsync(ConversationScope scope, CancellationToken cancellationToken = default);

    Task<VocabularySaveMode> SetModeAsync(
        ConversationScope scope,
        VocabularySaveMode mode,
        CancellationToken cancellationToken = default);
}
