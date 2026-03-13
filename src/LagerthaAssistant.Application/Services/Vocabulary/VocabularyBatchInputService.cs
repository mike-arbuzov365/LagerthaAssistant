namespace LagerthaAssistant.Application.Services.Vocabulary;

using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Vocabulary;

public sealed class VocabularyBatchInputService : IVocabularyBatchInputService
{
    public VocabularyBatchParseResult Parse(string rawInput, bool applySpaceSplitForSingleItem = false)
    {
        var parsedItems = VocabularyBatchInputParser.Parse(rawInput);

        if (parsedItems.Count == 0)
        {
            return new VocabularyBatchParseResult([], false, [], null);
        }

        var shouldOfferSpaceSplit = ShouldOfferSpaceSplit(rawInput, parsedItems);
        var splitCandidates = shouldOfferSpaceSplit
            ? SplitBySpaces(parsedItems[0])
            : [];

        var effectiveItems = applySpaceSplitForSingleItem && splitCandidates.Count > 1
            ? splitCandidates
            : parsedItems;

        return new VocabularyBatchParseResult(
            effectiveItems,
            shouldOfferSpaceSplit,
            splitCandidates,
            shouldOfferSpaceSplit ? parsedItems[0] : null);
    }

    private static bool ShouldOfferSpaceSplit(string rawInput, IReadOnlyList<string> parsedItems)
    {
        if (parsedItems.Count != 1)
        {
            return false;
        }

        var normalized = rawInput?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        if (normalized.Contains('\n', StringComparison.Ordinal)
            || normalized.Contains('\r', StringComparison.Ordinal)
            || normalized.Contains('\t', StringComparison.Ordinal)
            || normalized.Contains(';', StringComparison.Ordinal)
            || normalized.Contains(',', StringComparison.Ordinal)
            || normalized.Contains('.', StringComparison.Ordinal)
            || normalized.Contains('!', StringComparison.Ordinal)
            || normalized.Contains('?', StringComparison.Ordinal))
        {
            return false;
        }

        return normalized.Contains(' ', StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> SplitBySpaces(string input)
    {
        return input
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
