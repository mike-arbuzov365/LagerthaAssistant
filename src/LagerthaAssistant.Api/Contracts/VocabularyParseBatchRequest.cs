namespace LagerthaAssistant.Api.Contracts;

public sealed record VocabularyParseBatchRequest(
    string Input,
    bool ApplySpaceSplit = false);

public sealed record VocabularyParseBatchResponse(
    IReadOnlyList<string> Items,
    bool ShouldOfferSpaceSplit,
    IReadOnlyList<string> SpaceSplitCandidates,
    string? SingleItemWithoutSeparators);
