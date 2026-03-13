namespace LagerthaAssistant.Application.Models.Vocabulary;

public sealed record VocabularyBatchParseResult(
    IReadOnlyList<string> Items,
    bool ShouldOfferSpaceSplit,
    IReadOnlyList<string> SpaceSplitCandidates,
    string? SingleItemWithoutSeparators);
