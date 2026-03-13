namespace LagerthaAssistant.Api.Contracts;

public sealed record VocabularyAnalyzeRequest(
    string Input,
    string? Channel = null,
    string? UserId = null,
    string? ConversationId = null,
    string? ForcedDeckFileName = null,
    string? OverridePartOfSpeech = null,
    string? StorageMode = null);

public sealed record VocabularyAnalyzeBatchRequest(
    IReadOnlyList<string> Inputs,
    string? Channel = null,
    string? UserId = null,
    string? ConversationId = null,
    string? StorageMode = null);
