namespace LagerthaAssistant.Application.Services.Memory;

using System.Text.RegularExpressions;
using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.Interfaces.Memory;
using LagerthaAssistant.Application.Models.Memory;

public sealed class ConversationMemoryExtractor : IConversationMemoryExtractor
{
    private static readonly Regex MyNameIsRegex = new("\\bmy\\s+name\\s+is\\s+(?<name>[\\p{L}][\\p{L}\\-''’]{1,40})", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ImRegex = new("\\bi\\s+am\\s+(?<name>[\\p{L}][\\p{L}\\-''’]{1,40})", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex MeneZvatyRegex = new("\\b\\u043c\\u0435\\u043d\\u0435\\s+\\u0437\\u0432\\u0430\\u0442\\u0438\\s+(?<name>[\\p{L}][\\p{L}\\-''’]{1,40})", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public IReadOnlyCollection<MemoryFactCandidate> ExtractFromUserMessage(string userMessage)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            return [];
        }

        var facts = new List<MemoryFactCandidate>();

        var name = ExtractName(userMessage);
        if (!string.IsNullOrWhiteSpace(name))
        {
            facts.Add(new MemoryFactCandidate(MemoryKeys.UserName, name, 0.95));
        }

        var language = ExtractLanguagePreference(userMessage);
        if (!string.IsNullOrWhiteSpace(language))
        {
            facts.Add(new MemoryFactCandidate(MemoryKeys.PreferredLanguage, language, 0.90));
        }

        return facts;
    }

    private static string? ExtractName(string text)
    {
        var match = MeneZvatyRegex.Match(text);
        if (match.Success)
        {
            return match.Groups["name"].Value.Trim();
        }

        match = MyNameIsRegex.Match(text);
        if (match.Success)
        {
            return match.Groups["name"].Value.Trim();
        }

        match = ImRegex.Match(text);
        if (match.Success)
        {
            return match.Groups["name"].Value.Trim();
        }

        return null;
    }

    private static string? ExtractLanguagePreference(string text)
    {
        var normalized = text.Trim().ToLowerInvariant();

        if (normalized.Contains("\\u0443\\u043a\\u0440\\u0430\\u0457\\u043d\\u0441\\u044c\\u043a", StringComparison.Ordinal)
            || normalized.Contains("ukrainian", StringComparison.Ordinal))
        {
            return "uk";
        }

        if (normalized.Contains("english", StringComparison.Ordinal)
            || normalized.Contains("\\u0430\\u043d\\u0433\\u043b\\u0456\\u0439", StringComparison.Ordinal))
        {
            return "en";
        }

        return null;
    }
}

