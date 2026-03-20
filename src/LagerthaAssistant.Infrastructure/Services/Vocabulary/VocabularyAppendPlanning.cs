namespace LagerthaAssistant.Infrastructure.Services.Vocabulary;

using System.Text.RegularExpressions;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.Application.Services.Vocabulary;
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
    private static readonly char[] WordFormSeparators = ['-', ',', '/', '='];
    private static readonly Regex SentenceBoundaryRegex = new("(?<=[.!?])\\s+(?=[A-Z])", RegexOptions.Compiled);
    private static readonly Regex TightSentenceBoundaryRegex = new("(?<=[.!?])(?=[A-Z])", RegexOptions.Compiled);
    private static readonly Regex LowerToUpperBoundaryRegex = new("(?<=[a-z0-9])(?=[A-Z])", RegexOptions.Compiled);

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
        var targetWord = ResolveTargetWord(normalizedRequestedWord, normalizedParsedWord);

        if (string.IsNullOrWhiteSpace(targetWord))
        {
            return false;
        }

        var normalizedMeanings = NormalizeMeaningsWithOverridePos(parsedReply.Meanings, overridePartOfSpeech);
        var meaningText = JoinParagraphs(normalizedMeanings);
        var normalizedExamples = NormalizeExamples(parsedReply.Examples, normalizedMeanings.Count);
        var examplesText = JoinParagraphs(normalizedExamples);

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

        var translationText = JoinParagraphs(translationLines);
        return new VocabularyAppendPayload(expressionText, translationText, string.Empty);
    }

    private static string JoinParagraphs(IEnumerable<string> lines)
    {
        var normalized = lines
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(NormalizeExcelLineEndings)
            .ToList();

        return string.Join("\n\n", normalized);
    }

    private static string NormalizeExcelLineEndings(string line)
    {
        return line
            .Trim()
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> NormalizeExamples(IReadOnlyList<string> examples, int expectedCount)
    {
        if (examples.Count == 0)
        {
            return [];
        }

        var normalized = new List<string>();

        foreach (var example in examples)
        {
            if (string.IsNullOrWhiteSpace(example))
            {
                continue;
            }

            var lines = NormalizeExcelLineEndings(example)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed))
                {
                    normalized.Add(trimmed);
                }
            }
        }

        if (normalized.Count == 0 || expectedCount <= 1 || normalized.Count >= expectedCount)
        {
            return normalized;
        }

        var expanded = new List<string>();

        foreach (var line in normalized)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var split = SplitMergedExamples(line, expectedCount);
            expanded.AddRange(split);
        }

        return expanded.Count > 0 ? expanded : normalized;
    }

    private static IReadOnlyList<string> SplitMergedExamples(string line, int expectedCount)
    {
        var splitBySpaceAfterPunctuation = SentenceBoundaryRegex
            .Split(line)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        if (splitBySpaceAfterPunctuation.Count > 1)
        {
            return splitBySpaceAfterPunctuation;
        }

        var splitByTightPunctuation = TightSentenceBoundaryRegex
            .Split(line)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        if (splitByTightPunctuation.Count > 1)
        {
            return splitByTightPunctuation;
        }

        if (expectedCount <= 1)
        {
            return [line.Trim()];
        }

        var splitByLowerUpperBoundary = LowerToUpperBoundaryRegex
            .Split(line)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        if (splitByLowerUpperBoundary.Count > 1)
        {
            return splitByLowerUpperBoundary;
        }

        return [line.Trim()];
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
        return VocabularyPartOfSpeechCatalog.NormalizeOrNull(raw);
    }

    private static string ResolveTargetWord(string normalizedRequestedWord, string normalizedParsedWord)
    {
        if (string.IsNullOrWhiteSpace(normalizedParsedWord))
        {
            return NormalizeWord(normalizedRequestedWord);
        }

        if (string.IsNullOrWhiteSpace(normalizedRequestedWord))
        {
            return normalizedParsedWord;
        }

        if (string.Equals(normalizedRequestedWord, normalizedParsedWord, StringComparison.Ordinal))
        {
            return normalizedParsedWord;
        }

        if (IsSingleWord(normalizedRequestedWord)
            && !ContainsWordForm(normalizedParsedWord, normalizedRequestedWord))
        {
            return normalizedRequestedWord;
        }

        return normalizedParsedWord;
    }

    private static bool IsSingleWord(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return !value.Contains(' ', StringComparison.Ordinal);
    }

    private static bool ContainsWordForm(string parsedWord, string requestedWord)
    {
        if (string.IsNullOrWhiteSpace(parsedWord) || string.IsNullOrWhiteSpace(requestedWord))
        {
            return false;
        }

        if (string.Equals(parsedWord, requestedWord, StringComparison.Ordinal))
        {
            return true;
        }

        var forms = parsedWord
            .Split(WordFormSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return forms.Any(form => string.Equals(form, requestedWord, StringComparison.Ordinal));
    }
}
