namespace LagerthaAssistant.Application.Services.Vocabulary;

using System.Text.RegularExpressions;

public static class VocabularyBatchInputParser
{
    private static readonly Regex SentenceBoundaryRegex = new(@"(?<=[.!?])\s+", RegexOptions.Compiled);
    private static readonly Regex ListPrefixRegex = new(@"^(?:[-*\u2022]\s+|\d+[\.)]\s+)", RegexOptions.Compiled);

    public static IReadOnlyList<string> Parse(string rawInput)
    {
        if (string.IsNullOrWhiteSpace(rawInput))
        {
            return [];
        }

        var normalized = rawInput
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');

        var lines = normalized
            .Split('\n', StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(NormalizeToken)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        if (lines.Count == 0)
        {
            return [];
        }

        if (lines.Count > 1)
        {
            var multiLineTokens = new List<string>();
            foreach (var line in lines)
            {
                multiLineTokens.AddRange(SplitInline(line));
            }

            return DeduplicatePreservingOrder(multiLineTokens);
        }

        var singleLineTokens = SplitSingleLine(lines[0]);
        return DeduplicatePreservingOrder(singleLineTokens);
    }

    private static IReadOnlyList<string> SplitSingleLine(string line)
    {
        var inlineTokens = SplitInline(line);
        if (inlineTokens.Count > 1)
        {
            return inlineTokens;
        }

        if (LooksLikeSentenceSeries(line))
        {
            return SentenceBoundaryRegex
                .Split(line)
                .Select(NormalizeToken)
                .Where(token => !string.IsNullOrWhiteSpace(token))
                .ToList();
        }

        if (LooksLikeCommaList(line))
        {
            return line
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(NormalizeToken)
                .Where(token => !string.IsNullOrWhiteSpace(token))
                .ToList();
        }

        return [line];
    }

    private static IReadOnlyList<string> SplitInline(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        if (value.Contains('\t'))
        {
            return value
                .Split('\t', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(NormalizeToken)
                .Where(token => !string.IsNullOrWhiteSpace(token))
                .ToList();
        }

        if (value.Contains(';'))
        {
            return value
                .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(NormalizeToken)
                .Where(token => !string.IsNullOrWhiteSpace(token))
                .ToList();
        }

        return [value];
    }

    private static bool LooksLikeSentenceSeries(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = SentenceBoundaryRegex
            .Split(value)
            .Select(token => token.Trim())
            .Where(token => token.Length > 0)
            .ToList();

        return parts.Count >= 2 && parts.All(part => part.Any(char.IsLetter));
    }

    private static bool LooksLikeCommaList(string value)
    {
        var parts = value
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(token => token.Trim())
            .Where(token => token.Length > 0)
            .ToList();

        if (parts.Count < 2)
        {
            return false;
        }

        return parts.All(part =>
        {
            var wordCount = part.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
            return wordCount is > 0 and <= 4
                && !part.EndsWith(".", StringComparison.Ordinal)
                && !part.EndsWith("!", StringComparison.Ordinal)
                && !part.EndsWith("?", StringComparison.Ordinal);
        });
    }

    private static string NormalizeToken(string token)
    {
        var trimmed = token.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.Empty;
        }

        trimmed = ListPrefixRegex.Replace(trimmed, string.Empty).Trim();
        return trimmed;
    }

    private static IReadOnlyList<string> DeduplicatePreservingOrder(IEnumerable<string> tokens)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();

        foreach (var token in tokens)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            var normalized = token.Trim();
            if (seen.Add(normalized))
            {
                result.Add(normalized);
            }
        }

        return result;
    }
}
