namespace LagerthaAssistant.Api.Contracts;

public sealed record VocabularyDeckInfoResponse(
    string FileName,
    string FullPath,
    string? SuggestedPartOfSpeech);

public sealed record VocabularyDeckCatalogResponse(
    string StorageMode,
    IReadOnlyList<VocabularyDeckInfoResponse> Decks);
