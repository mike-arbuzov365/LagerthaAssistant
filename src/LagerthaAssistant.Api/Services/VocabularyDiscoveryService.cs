using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using LagerthaAssistant.Api.Interfaces;
using LagerthaAssistant.Application.Interfaces.AI;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Domain.AI;

namespace LagerthaAssistant.Api.Services;

public sealed class VocabularyDiscoveryService : IVocabularyDiscoveryService
{
    private const int MaxSourceChars = 120_000;
    private const int MaxTokenCandidates = 120;
    private const int MaxSuggestions = 40;

    private static readonly Regex HtmlTagRegex = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex HtmlScriptRegex = new("<script\\b[^<]*(?:(?!<\\/script>)<[^<]*)*<\\/script>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex HtmlStyleRegex = new("<style\\b[^<]*(?:(?!<\\/style>)<[^<]*)*<\\/style>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex TokenRegex = new("\\b[a-zA-Z][a-zA-Z'-]{2,}\\b", RegexOptions.Compiled);
    private static readonly Regex CodeFenceJsonRegex = new("```(?:json)?\\s*(?<json>[\\s\\S]+?)\\s*```", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "for", "that", "with", "this", "from", "have", "has", "had", "you", "your", "are", "was",
        "were", "will", "would", "can", "could", "should", "into", "onto", "over", "under", "about", "there",
        "their", "they", "them", "then", "than", "when", "what", "which", "while", "where", "after", "before",
        "because", "also", "just", "more", "most", "many", "much", "some", "such", "only", "very", "any", "all",
        "our", "out", "not", "but", "its", "it's", "his", "her", "she", "him", "who", "why", "how", "let", "lets"
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAiChatClient _aiChatClient;
    private readonly IVocabularyIndexService _vocabularyIndexService;
    private readonly IWordValidationService _wordValidationService;
    private readonly ILogger<VocabularyDiscoveryService> _logger;

    public VocabularyDiscoveryService(
        IHttpClientFactory httpClientFactory,
        IAiChatClient aiChatClient,
        IVocabularyIndexService vocabularyIndexService,
        IWordValidationService wordValidationService,
        ILogger<VocabularyDiscoveryService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _aiChatClient = aiChatClient;
        _vocabularyIndexService = vocabularyIndexService;
        _wordValidationService = wordValidationService;
        _logger = logger;
    }

    public async Task<VocabularyDiscoveryResult> DiscoverAsync(
        string sourceInput,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceInput))
        {
            return new VocabularyDiscoveryResult(
                VocabularyDiscoveryStatus.InvalidSource,
                [],
                "Source text is empty.");
        }

        var normalizedSource = sourceInput.Trim();
        var sourceWasUrl = TryParseHttpUrl(normalizedSource, out var sourceUri);

        string sourceText;
        try
        {
            sourceText = sourceWasUrl
                ? await ReadSourceTextFromUrlAsync(sourceUri!, cancellationToken)
                : normalizedSource;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load source text for vocabulary discovery.");
            return new VocabularyDiscoveryResult(
                VocabularyDiscoveryStatus.Failed,
                [],
                ex.Message,
                SourceWasUrl: sourceWasUrl);
        }

        var tokenFrequencies = ExtractCandidateTokens(sourceText);
        if (tokenFrequencies.Count == 0)
        {
            return new VocabularyDiscoveryResult(
                VocabularyDiscoveryStatus.NoCandidates,
                [],
                "No valid English words were found in the source.",
                SourceWasUrl: sourceWasUrl);
        }

        var knownLookups = await _vocabularyIndexService.FindByInputsAsync(tokenFrequencies.Keys.ToList(), cancellationToken);
        var unresolvedTokens = tokenFrequencies.Keys
            .Where(token => !knownLookups.TryGetValue(token, out var lookup) || !lookup.Found)
            .ToList();

        if (unresolvedTokens.Count == 0)
        {
            return new VocabularyDiscoveryResult(
                VocabularyDiscoveryStatus.NoCandidates,
                [],
                "All extracted words are already in the dictionary index.",
                SourceWasUrl: sourceWasUrl);
        }

        IReadOnlyList<VocabularyDiscoveryCandidate> classified;
        try
        {
            classified = await ClassifyCandidatesAsync(unresolvedTokens, tokenFrequencies, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI classification failed, falling back to heuristic classification.");
            classified = HeuristicClassify(unresolvedTokens, tokenFrequencies);
        }

        var filtered = classified
            .Where(candidate => candidate.PartOfSpeech is "n" or "v" or "adj")
            .GroupBy(candidate => candidate.Word, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(candidate => candidate.Frequency)
                .ThenBy(candidate => candidate.PartOfSpeech, StringComparer.Ordinal)
                .First())
            .OrderByDescending(candidate => candidate.Frequency)
            .ThenBy(candidate => candidate.Word, StringComparer.Ordinal)
            .Take(MaxSuggestions)
            .ToList();

        if (filtered.Count == 0)
        {
            return new VocabularyDiscoveryResult(
                VocabularyDiscoveryStatus.NoCandidates,
                [],
                "No noun/verb/adjective candidates were found.",
                SourceWasUrl: sourceWasUrl);
        }

        return new VocabularyDiscoveryResult(
            VocabularyDiscoveryStatus.Success,
            filtered,
            "ok",
            SourceWasUrl: sourceWasUrl);
    }

    private async Task<string> ReadSourceTextFromUrlAsync(Uri sourceUri, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("vocab-discovery");
        using var request = new HttpRequestMessage(HttpMethod.Get, sourceUri);
        request.Headers.UserAgent.ParseAdd("LagerthaAssistant/1.0");

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Failed to download URL ({(int)response.StatusCode}).");
        }

        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidOperationException("Downloaded page is empty.");
        }

        if (raw.Length > MaxSourceChars)
        {
            raw = raw[..MaxSourceChars];
        }

        var withoutScripts = HtmlScriptRegex.Replace(raw, " ");
        var withoutStyles = HtmlStyleRegex.Replace(withoutScripts, " ");
        var withoutTags = HtmlTagRegex.Replace(withoutStyles, " ");
        return WebUtility.HtmlDecode(withoutTags);
    }

    private static bool TryParseHttpUrl(string value, out Uri? uri)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var parsed)
            && (string.Equals(parsed.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                || string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            uri = parsed;
            return true;
        }

        uri = null;
        return false;
    }

    private Dictionary<string, int> ExtractCandidateTokens(string sourceText)
    {
        var frequencies = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(sourceText))
        {
            return frequencies;
        }

        var normalizedText = sourceText.Length > MaxSourceChars
            ? sourceText[..MaxSourceChars]
            : sourceText;

        foreach (Match match in TokenRegex.Matches(normalizedText))
        {
            if (!match.Success)
            {
                continue;
            }

            var token = match.Value.Trim().ToLowerInvariant();
            if (token.Length < 3
                || token.Length > 30
                || StopWords.Contains(token))
            {
                continue;
            }

            if (!_wordValidationService.IsValidWord(token))
            {
                continue;
            }

            frequencies[token] = frequencies.TryGetValue(token, out var count)
                ? count + 1
                : 1;
        }

        return frequencies
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .Take(MaxTokenCandidates)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<IReadOnlyList<VocabularyDiscoveryCandidate>> ClassifyCandidatesAsync(
        IReadOnlyList<string> tokens,
        IReadOnlyDictionary<string, int> tokenFrequencies,
        CancellationToken cancellationToken)
    {
        if (tokens.Count == 0)
        {
            return [];
        }

        var now = DateTimeOffset.UtcNow;
        var messages = new List<ConversationMessage>
        {
            ConversationMessage.Create(
                MessageRole.System,
                "You classify English words by primary part of speech. Allowed POS values: n, v, adj. " +
                "Return strict JSON only: [{\"word\":\"token\",\"pos\":\"n|v|adj\"}]. " +
                "Use each token at most once. Do not include any comments or markdown.",
                now),
            ConversationMessage.Create(
                MessageRole.User,
                "Classify the following tokens by their most common primary POS:\n" +
                string.Join('\n', tokens.Select(token => $"{token}|{tokenFrequencies[token]}")),
                now)
        };

        var completion = await _aiChatClient.CompleteAsync(messages, cancellationToken);
        var raw = completion.Content?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return HeuristicClassify(tokens, tokenFrequencies);
        }

        var json = ExtractJson(raw);
        using var document = JsonDocument.Parse(json);

        var items = new List<VocabularyDiscoveryCandidate>();
        var root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Array)
        {
            ParseJsonItems(root, items, tokenFrequencies);
        }
        else if (root.ValueKind == JsonValueKind.Object
                 && root.TryGetProperty("items", out var itemsProperty)
                 && itemsProperty.ValueKind == JsonValueKind.Array)
        {
            ParseJsonItems(itemsProperty, items, tokenFrequencies);
        }

        if (items.Count == 0)
        {
            return HeuristicClassify(tokens, tokenFrequencies);
        }

        return items;
    }

    private static void ParseJsonItems(
        JsonElement jsonItems,
        ICollection<VocabularyDiscoveryCandidate> output,
        IReadOnlyDictionary<string, int> frequencies)
    {
        foreach (var item in jsonItems.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (!item.TryGetProperty("word", out var wordElement)
                || wordElement.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(wordElement.GetString()))
            {
                continue;
            }

            if (!item.TryGetProperty("pos", out var posElement)
                || posElement.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(posElement.GetString()))
            {
                continue;
            }

            var word = wordElement.GetString()!.Trim().ToLowerInvariant();
            var pos = posElement.GetString()!.Trim().ToLowerInvariant();
            if (pos is not ("n" or "v" or "adj"))
            {
                continue;
            }

            if (!frequencies.TryGetValue(word, out var frequency))
            {
                continue;
            }

            output.Add(new VocabularyDiscoveryCandidate(word, pos, frequency));
        }
    }

    private static IReadOnlyList<VocabularyDiscoveryCandidate> HeuristicClassify(
        IReadOnlyList<string> tokens,
        IReadOnlyDictionary<string, int> frequencies)
    {
        var result = new List<VocabularyDiscoveryCandidate>(tokens.Count);
        foreach (var token in tokens)
        {
            if (!frequencies.TryGetValue(token, out var frequency))
            {
                continue;
            }

            var pos = GuessPartOfSpeech(token);
            if (pos is not ("n" or "v" or "adj"))
            {
                continue;
            }

            result.Add(new VocabularyDiscoveryCandidate(token, pos, frequency));
        }

        return result;
    }

    private static string GuessPartOfSpeech(string token)
    {
        if (token.EndsWith("ing", StringComparison.Ordinal)
            || token.EndsWith("ed", StringComparison.Ordinal)
            || token.EndsWith("ize", StringComparison.Ordinal)
            || token.EndsWith("ise", StringComparison.Ordinal)
            || token.EndsWith("ify", StringComparison.Ordinal)
            || token.EndsWith("ate", StringComparison.Ordinal))
        {
            return "v";
        }

        if (token.EndsWith("ous", StringComparison.Ordinal)
            || token.EndsWith("ive", StringComparison.Ordinal)
            || token.EndsWith("ful", StringComparison.Ordinal)
            || token.EndsWith("able", StringComparison.Ordinal)
            || token.EndsWith("ible", StringComparison.Ordinal)
            || token.EndsWith("less", StringComparison.Ordinal)
            || token.EndsWith("al", StringComparison.Ordinal)
            || token.EndsWith("ic", StringComparison.Ordinal)
            || token.EndsWith("ary", StringComparison.Ordinal))
        {
            return "adj";
        }

        return "n";
    }

    private static string ExtractJson(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "[]";
        }

        var match = CodeFenceJsonRegex.Match(raw);
        if (match.Success && match.Groups["json"].Success)
        {
            return match.Groups["json"].Value.Trim();
        }

        var trimmed = raw.Trim();
        var firstArray = trimmed.IndexOf('[');
        var lastArray = trimmed.LastIndexOf(']');
        if (firstArray >= 0 && lastArray > firstArray)
        {
            return trimmed[firstArray..(lastArray + 1)];
        }

        var firstObject = trimmed.IndexOf('{');
        var lastObject = trimmed.LastIndexOf('}');
        if (firstObject >= 0 && lastObject > firstObject)
        {
            return trimmed[firstObject..(lastObject + 1)];
        }

        return "[]";
    }
}
