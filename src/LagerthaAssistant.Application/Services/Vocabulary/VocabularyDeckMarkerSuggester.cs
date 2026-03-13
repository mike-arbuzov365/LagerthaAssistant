namespace LagerthaAssistant.Application.Services.Vocabulary;

public static class VocabularyDeckMarkerSuggester
{
    public static string? SuggestMarker(string? deckFileName)
    {
        if (string.IsNullOrWhiteSpace(deckFileName))
        {
            return null;
        }

        var name = deckFileName.ToLowerInvariant();

        if ((name.Contains("persistent", StringComparison.Ordinal) || name.Contains("persistant", StringComparison.Ordinal))
            && name.Contains("expression", StringComparison.Ordinal))
        {
            return "pe";
        }

        if (name.Contains("phrasal", StringComparison.Ordinal))
        {
            return "pv";
        }

        if (name.Contains("irregular", StringComparison.Ordinal) && name.Contains("verb", StringComparison.Ordinal))
        {
            return "iv";
        }

        if (name.Contains("adjective", StringComparison.Ordinal))
        {
            return "adj";
        }

        if (name.Contains("adverb", StringComparison.Ordinal))
        {
            return "adv";
        }

        if (name.Contains("pronoun", StringComparison.Ordinal))
        {
            return "pron";
        }

        if (name.Contains("preposition", StringComparison.Ordinal))
        {
            return "prep";
        }

        if (name.Contains("conjunction", StringComparison.Ordinal))
        {
            return "conj";
        }

        if (name.Contains("noun", StringComparison.Ordinal))
        {
            return "n";
        }

        if (name.Contains("verb", StringComparison.Ordinal))
        {
            return "v";
        }

        return null;
    }
}
