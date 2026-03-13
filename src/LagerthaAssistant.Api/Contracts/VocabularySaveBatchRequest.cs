namespace LagerthaAssistant.Api.Contracts;

public sealed record VocabularySaveBatchItemRequest(
    string RequestedWord,
    string AssistantReply,
    string? ForcedDeckFileName = null,
    string? OverridePartOfSpeech = null);

public sealed record VocabularySaveBatchRequest(
    IReadOnlyList<VocabularySaveBatchItemRequest> Items);

public sealed record VocabularySaveBatchItemResponse(
    int Index,
    string RequestedWord,
    VocabularyAppendResultResponse Result);

public sealed record VocabularySaveBatchResponse(
    int Total,
    int Added,
    int Duplicates,
    int Failed,
    IReadOnlyList<VocabularySaveBatchItemResponse> Items);
