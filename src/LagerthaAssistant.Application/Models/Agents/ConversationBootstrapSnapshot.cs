namespace LagerthaAssistant.Application.Models.Agents;

using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.Application.Services.Vocabulary;

public sealed record ConversationBootstrapSnapshot(
    ConversationScope Scope,
    string SaveMode,
    IReadOnlyList<string> AvailableSaveModes,
    string StorageMode,
    IReadOnlyList<string> AvailableStorageModes,
    GraphAuthStatus Graph,
    IReadOnlyList<ConversationCommandCatalogGroup> CommandGroups,
    IReadOnlyList<VocabularyPartOfSpeechOption> PartOfSpeechOptions);
