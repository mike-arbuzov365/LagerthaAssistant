namespace LagerthaAssistant.Infrastructure.Services.Vocabulary;

using System.Reflection;
using LagerthaAssistant.Application.Interfaces.Vocabulary;

public sealed class WordValidationService : IWordValidationService
{
    private readonly HashSet<string> _words;

    public WordValidationService()
    {
        _words = LoadWords();
    }

    public bool IsValidWord(string word)
    {
        if (string.IsNullOrWhiteSpace(word))
            return false;

        return _words.Contains(word.ToLowerInvariant());
    }

    public IReadOnlyList<string> GetSuggestions(string word, int maxCount = 5)
    {
        if (string.IsNullOrWhiteSpace(word) || word.Length < 2)
            return [];

        var lower = word.ToLowerInvariant();
        var minLen = Math.Max(1, lower.Length - 2);
        var maxLen = lower.Length + 2;
        var firstChar = lower[0];

        var candidates = _words
            .Where(w => w.Length >= minLen && w.Length <= maxLen && w[0] == firstChar)
            .Select(w => (Word: w, Distance: ComputeEditDistance(lower, w)))
            .Where(x => x.Distance <= 2)
            .OrderBy(x => x.Distance)
            .ThenBy(x => x.Word)
            .Take(maxCount)
            .Select(x => x.Word)
            .ToList();

        return candidates;
    }

    private static HashSet<string> LoadWords()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("words_en_us.txt", StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
            return [];

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);

        var words = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var trimmed = line.Trim();
            if (!string.IsNullOrEmpty(trimmed))
                words.Add(trimmed.ToLowerInvariant());
        }

        return words;
    }

    private static int ComputeEditDistance(string a, string b)
    {
        if (a == b) return 0;
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;

        var prev = new int[b.Length + 1];
        var curr = new int[b.Length + 1];

        for (var j = 0; j <= b.Length; j++)
            prev[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            curr[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                curr[j] = Math.Min(
                    Math.Min(curr[j - 1] + 1, prev[j] + 1),
                    prev[j - 1] + cost);
            }
            Array.Copy(curr, prev, b.Length + 1);
        }

        return prev[b.Length];
    }
}
