namespace LagerthaAssistant.Application.Services.Vocabulary;

using System.Text.RegularExpressions;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Vocabulary;

public sealed class VocabularyReplyParser : IVocabularyReplyParser
{
    private static readonly Regex MeaningLineRegex = new("^\\((?<pos>[a-z]+)\\)\\s+.+$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public bool TryParse(string assistantReply, out ParsedVocabularyReply? parsedReply)
    {
        parsedReply = null;

        if (string.IsNullOrWhiteSpace(assistantReply))
        {
            return false;
        }

        var normalized = NormalizeAssistantReply(assistantReply);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        var lines = normalized
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Select(line => line.TrimEnd())
            .ToList();

        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[0]))
        {
            lines.RemoveAt(0);
        }

        if (lines.Count == 0)
        {
            return false;
        }

        var word = lines[0].Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(word))
        {
            return false;
        }

        var meanings = new List<string>();
        var examples = new List<string>();
        var partsOfSpeech = new List<string>();
        var seenPartsOfSpeech = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawLine in lines.Skip(1))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var match = MeaningLineRegex.Match(line);
            if (match.Success)
            {
                meanings.Add(line);

                var partOfSpeech = match.Groups["pos"].Value.ToLowerInvariant();
                if (seenPartsOfSpeech.Add(partOfSpeech))
                {
                    partsOfSpeech.Add(partOfSpeech);
                }

                continue;
            }

            examples.Add(line);
        }

        if (meanings.Count == 0)
        {
            return false;
        }

        var allowsNoExamples = partsOfSpeech.Any(pos => pos.Equals("pe", StringComparison.OrdinalIgnoreCase));
        if (!allowsNoExamples && examples.Count == 0)
        {
            return false;
        }

        parsedReply = new ParsedVocabularyReply(word, meanings, examples, partsOfSpeech);
        return true;
    }

    private static string NormalizeAssistantReply(string assistantReply)
    {
        var trimmed = assistantReply.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var firstNewLine = trimmed.IndexOf('\n');
        if (firstNewLine < 0)
        {
            return trimmed;
        }

        var closingFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
        if (closingFence <= firstNewLine)
        {
            return trimmed;
        }

        return trimmed[(firstNewLine + 1)..closingFence].Trim();
    }
}
