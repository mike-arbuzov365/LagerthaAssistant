namespace LagerthaAssistant.Application.Services.Vocabulary;

public sealed record VocabularyPartOfSpeechOption(
    int Number,
    string Marker,
    string Label,
    IReadOnlyList<string> Aliases);

public static class VocabularyPartOfSpeechCatalog
{
    private static readonly IReadOnlyList<VocabularyPartOfSpeechOption> Options = new List<VocabularyPartOfSpeechOption>
    {
        Create(1, "n", "noun", "noun"),
        Create(2, "v", "verb", "verb"),
        Create(3, "iv", "irregular verb", "irregular", "irregular-verb"),
        Create(4, "pv", "phrasal verb", "phrasal", "phrasal-verb"),
        Create(5, "adj", "adjective", "adjective"),
        Create(6, "adv", "adverb", "adverb"),
        Create(7, "prep", "preposition", "preposition"),
        Create(8, "conj", "conjunction", "conjunction"),
        Create(9, "pron", "pronoun", "pronoun"),
        Create(10, "pe", "persistent expression", "persistent", "persistent-expression", "persistant-expression", "expression")
    };

    private static readonly IReadOnlyDictionary<string, string> AliasToMarker = BuildAliasMap(Options);

    public static IReadOnlyList<VocabularyPartOfSpeechOption> GetOptions()
    {
        return Options;
    }

    public static bool TryNormalize(string? raw, out string marker)
    {
        marker = string.Empty;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var normalized = raw.Trim().ToLowerInvariant();
        if (!AliasToMarker.TryGetValue(normalized, out var resolvedMarker)
            || string.IsNullOrWhiteSpace(resolvedMarker))
        {
            return false;
        }

        marker = resolvedMarker;
        return true;
    }

    public static string? NormalizeOrNull(string? raw)
    {
        return TryNormalize(raw, out var marker)
            ? marker
            : null;
    }

    private static VocabularyPartOfSpeechOption Create(
        int number,
        string marker,
        string label,
        params string[] aliases)
    {
        var normalizedAliases = aliases
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .Select(alias => alias.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (!normalizedAliases.Contains(marker, StringComparer.Ordinal))
        {
            normalizedAliases.Insert(0, marker);
        }

        var numberAlias = number.ToString();
        if (!normalizedAliases.Contains(numberAlias, StringComparer.Ordinal))
        {
            normalizedAliases.Add(numberAlias);
        }

        return new VocabularyPartOfSpeechOption(number, marker, label, normalizedAliases);
    }

    private static IReadOnlyDictionary<string, string> BuildAliasMap(IReadOnlyList<VocabularyPartOfSpeechOption> options)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var option in options)
        {
            foreach (var alias in option.Aliases)
            {
                map[alias] = option.Marker;
            }
        }

        return map;
    }
}
