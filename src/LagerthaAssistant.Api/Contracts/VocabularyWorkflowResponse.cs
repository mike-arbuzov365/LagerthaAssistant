namespace LagerthaAssistant.Api.Contracts;

public sealed record VocabularyDeckEntryResponse(
    string DeckFileName,
    string DeckPath,
    int RowNumber,
    string Word,
    string Meaning,
    string Examples);

public sealed record VocabularyLookupResponse(
    string Query,
    bool Found,
    IReadOnlyList<VocabularyDeckEntryResponse> Matches);

public sealed record VocabularyAssistantUsageResponse(
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens);

public sealed record VocabularyAssistantCompletionResponse(
    string Content,
    string Model,
    VocabularyAssistantUsageResponse? Usage);

public sealed record VocabularyAppendPreviewResponse(
    string Status,
    string Word,
    string? TargetDeckFileName,
    string? TargetDeckPath,
    IReadOnlyList<VocabularyDeckEntryResponse>? DuplicateMatches,
    string? Message);

public sealed record VocabularyWorkflowItemResponse(
    string Input,
    bool FoundInDeck,
    VocabularyLookupResponse Lookup,
    VocabularyAssistantCompletionResponse? AssistantCompletion,
    VocabularyAppendPreviewResponse? AppendPreview);

public sealed record VocabularyAppendResultResponse(
    string Status,
    VocabularyDeckEntryResponse? Entry,
    IReadOnlyList<VocabularyDeckEntryResponse>? DuplicateMatches,
    string? Message);

