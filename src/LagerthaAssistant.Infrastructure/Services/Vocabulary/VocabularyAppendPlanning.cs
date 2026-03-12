namespace LagerthaAssistant.Infrastructure.Services.Vocabulary;

using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.Infrastructure.Options;

internal readonly record struct VocabularyAppendRequestSignature(
    string RequestedWord,
    string AssistantReply,
    string? ForcedDeckFileName,
    string? OverridePartOfSpeech);

internal readonly record struct VocabularyAppendPayload(
    string TargetWord,
    string MeaningText,
    string ExamplesText);

internal static class VocabularyAppendPlanning
{
    public static string NormalizeWord(string value)
    {
        return value.Trim().ToLowerInvariant();
    }

    public static VocabularyAppendRequestSignature CreateSignature(
        string requestedWord,
        string assistantReply,
        string? forcedDeckFileName,
        string? overridePartOfSpeech)
    {
        return new VocabularyAppendRequestSignature(
            NormalizeWord(requestedWord),
            assistantReply.Trim(),
            NormalizeOptional(forcedDeckFileName),
            NormalizeOptional(overridePartOfSpeech));
    }

    public static bool TryBuildPayload(
        IVocabularyReplyParser replyParser,
        string requestedWord,
        string assistantReply,
        string? overridePartOfSpeech,
        out VocabularyAppendPayload payload)
    {
        payload = default;

        if (!replyParser.TryParse(assistantReply, out var parsedReply) || parsedReply is null)
        {
            return false;
        }

        return TryBuildPayload(NormalizeWord(requestedWord), parsedReply, overridePartOfSpeech, out payload);
    }

    public static bool TryBuildPayload(
        string normalizedRequestedWord,
        ParsedVocabularyReply? parsedReply,
        string? overridePartOfSpeech,
        out VocabularyAppendPayload payload)
    {
        payload = default;

        if (parsedReply is null)
        {
            return false;
        }

        var normalizedParsedWord = NormalizeWord(parsedReply.Word);
        var targetWord = string.IsNullOrWhiteSpace(normalizedParsedWord)
            ? NormalizeWord(normalizedRequestedWord)
            : normalizedParsedWord;

        if (string.IsNullOrWhiteSpace(targetWord))
        {
            return false;
        }

        var normalizedMeanings = NormalizeMeaningsWithOverridePos(parsedReply.Meanings, overridePartOfSpeech);
        var meaningText = string.Join(Environment.NewLine + Environment.NewLine, normalizedMeanings);
        var examplesText = string.Join(Environment.NewLine + Environment.NewLine, parsedReply.Examples);

        payload = new VocabularyAppendPayload(targetWord, meaningText, examplesText);
        return true;
    }

    public static VocabularyAppendPayload ApplyDeckSpecificProfile(
        VocabularyAppendPayload payload,
        ParsedVocabularyReply parsedReply,
        string requestedWord,
        string targetDeckFileName,
        VocabularyDeckOptions options)
    {
        if (string.IsNullOrWhiteSpace(targetDeckFileName))
        {
            return payload;
        }

        if (targetDeckFileName.Equals(options.PersistentExpressionDeckFileName, StringComparison.OrdinalIgnoreCase))
        {
            return ApplyPersistentExpressionProfile(payload, parsedReply, requestedWord);
        }

        return payload;
    }

    private static VocabularyAppendPayload ApplyPersistentExpressionProfile(
        VocabularyAppendPayload payload,
        ParsedVocabularyReply parsedReply,
        string requestedWord)
    {
        var expressionText = NormalizeExpressionText(requestedWord);
        if (string.IsNullOrWhiteSpace(expressionText))
        {
            expressionText = NormalizeExpressionText(parsedReply.Word);
        }

        if (string.IsNullOrWhiteSpace(expressionText))
        {
            expressionText = payload.TargetWord;
        }

        var translationLines = parsedReply.Meanings
            .Select(ExtractMeaningBody)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => $"(pe) {line}")
            .ToList();

        if (translationLines.Count == 0 && !string.IsNullOrWhiteSpace(payload.MeaningText))
        {
            var fallback = ExtractMeaningBody(payload.MeaningText);
            if (!string.IsNullOrWhiteSpace(fallback))
            {
                translationLines.Add($"(pe) {fallback}");
            }
        }

        var translationText = string.Join(Environment.NewLine + Environment.NewLine, translationLines);
        return new VocabularyAppendPayload(expressionText, translationText, string.Empty);
    }

    private static string NormalizeExpressionText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalizedSpaces = string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        if (string.IsNullOrWhiteSpace(normalizedSpaces))
        {
            return string.Empty;
        }

        var first = normalizedSpaces[0];
        if (!char.IsLetter(first))
        {
            return normalizedSpaces;
        }

        var capitalizedFirst = char.ToUpperInvariant(first);
        return normalizedSpaces.Length == 1
            ? capitalizedFirst.ToString()
            : capitalizedFirst + normalizedSpaces[1..];
    }

    private static string ExtractMeaningBody(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        if (!trimmed.StartsWith('('))
        {
            return trimmed;
        }

        var closeIndex = trimmed.IndexOf(')');
        return closeIndex >= 0
            ? trimmed[(closeIndex + 1)..].TrimStart()
            : trimmed.Trim('(', ')', ' ');
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().ToLowerInvariant();
    }

    private static IReadOnlyList<string> NormalizeMeaningsWithOverridePos(
        IReadOnlyList<string> meanings,
        string? overridePartOfSpeech)
    {
        var normalizedPos = NormalizePartOfSpeech(overridePartOfSpeech);
        if (string.IsNullOrWhiteSpace(normalizedPos))
        {
            return meanings;
        }

        var adjusted = new List<string>(meanings.Count);

        foreach (var meaning in meanings)
        {
            var trimmed = meaning?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                continue;
            }

            if (trimmed.StartsWith("(", StringComparison.Ordinal))
            {
                var closeIndex = trimmed.IndexOf(')');
                if (closeIndex > 0)
                {
                    adjusted.Add($"({normalizedPos}) {trimmed[(closeIndex + 1)..].TrimStart()}");
                }
                else
                {
                    adjusted.Add($"({normalizedPos}) {trimmed.Trim('(', ')', ' ')}");
                }
            }
            else
            {
                adjusted.Add($"({normalizedPos}) {trimmed}");
            }
        }

        return adjusted.Count > 0 ? adjusted : meanings;
    }

    private static string? NormalizePartOfSpeech(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return raw.Trim().ToLowerInvariant() switch
        {
            "n" or "noun" => "n",
            "v" or "verb" => "v",
            "iv" or "irregular" or "irregular-verb" => "iv",
            "pv" or "phrasal" or "phrasal-verb" => "pv",
            "adj" or "adjective" => "adj",
            "adv" or "adverb" => "adv",
            "prep" or "preposition" => "prep",
            "conj" or "conjunction" => "conj",
            "pron" or "pronoun" => "pron",
            "pe" or "persistent" or "persistent-expression" or "expression" => "pe",
            _ => null
        };
    }
}