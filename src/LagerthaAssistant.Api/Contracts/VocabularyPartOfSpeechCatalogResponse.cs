namespace LagerthaAssistant.Api.Contracts;

public sealed record VocabularyPartOfSpeechOptionResponse(
    int Number,
    string Marker,
    string Label);

public sealed record VocabularyPartOfSpeechCatalogResponse(
    IReadOnlyList<VocabularyPartOfSpeechOptionResponse> Markers);
