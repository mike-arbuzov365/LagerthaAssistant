namespace LagerthaAssistant.Application.Services.Vocabulary;

using LagerthaAssistant.Application.Interfaces.AI;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Vocabulary;

public sealed class VocabularyWorkflowService : IVocabularyWorkflowService
{
    private readonly IAssistantSessionService _assistantSessionService;
    private readonly IVocabularyDeckService _vocabularyDeckService;
    private readonly IVocabularyIndexService? _vocabularyIndexService;
    private readonly IVocabularyStorageModeProvider? _storageModeProvider;
    private readonly IWordValidationService? _wordValidationService;

    public VocabularyWorkflowService(
        IAssistantSessionService assistantSessionService,
        IVocabularyDeckService vocabularyDeckService,
        IVocabularyIndexService? vocabularyIndexService = null,
        IVocabularyStorageModeProvider? storageModeProvider = null,
        IWordValidationService? wordValidationService = null)
    {
        _assistantSessionService = assistantSessionService;
        _vocabularyDeckService = vocabularyDeckService;
        _vocabularyIndexService = vocabularyIndexService;
        _storageModeProvider = storageModeProvider;
        _wordValidationService = wordValidationService;
    }

    public async Task<VocabularyWorkflowItemResult> ProcessAsync(
        string input,
        string? forcedDeckFileName = null,
        string? overridePartOfSpeech = null,
        bool bypassValidation = false,
        CancellationToken cancellationToken = default)
    {
        return await ProcessCoreAsync(
            input,
            forcedDeckFileName,
            overridePartOfSpeech,
            bypassValidation,
            skipIndexLookup: false,
            cancellationToken);
    }

    public async Task<IReadOnlyList<VocabularyWorkflowItemResult>> ProcessBatchAsync(
        IReadOnlyList<string> inputs,
        CancellationToken cancellationToken = default)
    {
        if (inputs is null)
        {
            throw new ArgumentNullException(nameof(inputs));
        }

        var normalizedInputs = inputs
            .Where(input => !string.IsNullOrWhiteSpace(input))
            .Select(input => input.Trim())
            .ToList();

        if (normalizedInputs.Count == 0)
        {
            return [];
        }

        var indexedLookups = _vocabularyIndexService is null
            ? null
            : await _vocabularyIndexService.FindByInputsAsync(normalizedInputs, cancellationToken);

        var unresolvedInputs = normalizedInputs
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(input => indexedLookups is null
                || !indexedLookups.TryGetValue(input, out var indexedLookup)
                || !indexedLookup.Found)
            .ToList();

        IReadOnlyDictionary<string, VocabularyLookupResult>? deckLookups = null;
        if (unresolvedInputs.Count > 0
            && _vocabularyDeckService is IVocabularyBatchDeckLookupService batchDeckLookupService)
        {
            deckLookups = await batchDeckLookupService.FindInWritableDecksBatchAsync(unresolvedInputs, cancellationToken);
        }

        var results = new List<VocabularyWorkflowItemResult>(normalizedInputs.Count);
        var perInputCache = new Dictionary<string, VocabularyWorkflowItemResult>(StringComparer.OrdinalIgnoreCase);
        var mode = _storageModeProvider?.CurrentMode ?? VocabularyStorageMode.Local;

        foreach (var input in normalizedInputs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (perInputCache.TryGetValue(input, out var cached))
            {
                results.Add(CloneForInput(cached, input));
                continue;
            }

            if (indexedLookups is not null
                && indexedLookups.TryGetValue(input, out var indexedLookup)
                && indexedLookup.Found)
            {
                var indexedResult = new VocabularyWorkflowItemResult(input, indexedLookup);
                perInputCache[input] = indexedResult;
                results.Add(indexedResult);
                continue;
            }

            if (deckLookups is not null
                && deckLookups.TryGetValue(input, out var deckLookup))
            {
                if (deckLookup.Found)
                {
                    if (_vocabularyIndexService is not null)
                    {
                        await _vocabularyIndexService.IndexLookupResultAsync(deckLookup, mode, cancellationToken);
                    }

                    var deckResult = new VocabularyWorkflowItemResult(input, deckLookup);
                    perInputCache[input] = deckResult;
                    results.Add(deckResult);
                    continue;
                }

                var aiResult = await BuildAiResultAsync(input, cancellationToken);
                perInputCache[input] = aiResult;
                results.Add(aiResult);
                continue;
            }

            var result = await ProcessCoreAsync(
                input,
                forcedDeckFileName: null,
                overridePartOfSpeech: null,
                bypassValidation: false,
                skipIndexLookup: indexedLookups is not null,
                cancellationToken);
            perInputCache[input] = result;
            results.Add(result);
        }

        return results;
    }

    private async Task<VocabularyWorkflowItemResult> ProcessCoreAsync(
        string input,
        string? forcedDeckFileName,
        string? overridePartOfSpeech,
        bool bypassValidation,
        bool skipIndexLookup,
        CancellationToken cancellationToken = default)
    {
        var normalizedInput = input?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedInput))
        {
            throw new ArgumentException("Input cannot be empty.", nameof(input));
        }

        if (!bypassValidation && _wordValidationService is not null)
        {
            var lower = normalizedInput.ToLowerInvariant();
            if (!lower.Contains(' ') && !_wordValidationService.IsValidWord(lower))
            {
                var suggestions = _wordValidationService.GetSuggestions(lower);
                return new VocabularyWorkflowItemResult(normalizedInput, new VocabularyLookupResult(normalizedInput, []))
                {
                    IsWordUnrecognized = true,
                    WordSuggestions = suggestions
                };
            }
        }

        if (!skipIndexLookup && _vocabularyIndexService is not null)
        {
            var indexedLookup = await _vocabularyIndexService.FindByInputAsync(normalizedInput, cancellationToken);
            if (indexedLookup.Found)
            {
                // Validate: filter out stale entries where the stored word is not a legitimate
                // match for the query (can happen if fuzzy-matched results were previously indexed).
                var validatedMatches = indexedLookup.Matches
                    .Where(m => IsWordFormMatch(m.Word, normalizedInput))
                    .ToList();

                if (validatedMatches.Count > 0)
                {
                    var validatedLookup = new VocabularyLookupResult(normalizedInput, validatedMatches);
                    return new VocabularyWorkflowItemResult(normalizedInput, validatedLookup);
                }
            }
        }

        var lookup = await _vocabularyDeckService.FindInWritableDecksAsync(normalizedInput, cancellationToken);
        if (lookup.Found)
        {
            if (_vocabularyIndexService is not null)
            {
                var mode = _storageModeProvider?.CurrentMode ?? VocabularyStorageMode.Local;
                await _vocabularyIndexService.IndexLookupResultAsync(lookup, mode, cancellationToken);
            }

            return new VocabularyWorkflowItemResult(normalizedInput, lookup);
        }

        var completion = await _assistantSessionService.AskAsync(normalizedInput, cancellationToken);
        var preview = await _vocabularyDeckService.PreviewAppendFromAssistantReplyAsync(
            normalizedInput,
            completion.Content,
            forcedDeckFileName,
            overridePartOfSpeech,
            cancellationToken);

        return new VocabularyWorkflowItemResult(normalizedInput, lookup, completion, preview);
    }

    private async Task<VocabularyWorkflowItemResult> BuildAiResultAsync(
        string normalizedInput,
        CancellationToken cancellationToken)
    {
        var lookup = new VocabularyLookupResult(normalizedInput, []);
        var completion = await _assistantSessionService.AskAsync(normalizedInput, cancellationToken);
        var preview = await _vocabularyDeckService.PreviewAppendFromAssistantReplyAsync(
            normalizedInput,
            completion.Content,
            forcedDeckFileName: null,
            overridePartOfSpeech: null,
            cancellationToken);

        return new VocabularyWorkflowItemResult(normalizedInput, lookup, completion, preview);
    }

    /// <summary>
    /// Returns true if <paramref name="query"/> is an exact match or a word form of <paramref name="storedWord"/>.
    /// Word forms are separated by " - " (e.g. "go - went - gone").
    /// </summary>
    private static bool IsWordFormMatch(string storedWord, string query)
    {
        if (string.IsNullOrWhiteSpace(storedWord) || string.IsNullOrWhiteSpace(query))
        {
            return false;
        }

        var normalized = storedWord.Trim().ToLowerInvariant();
        var normalizedQuery = query.Trim().ToLowerInvariant();

        if (normalized.Equals(normalizedQuery, StringComparison.Ordinal))
        {
            return true;
        }

        // Check individual word forms (e.g. "go - went - gone")
        var separators = new[] { " - ", ", ", "," };
        var forms = normalized.Split(separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return forms.Any(f => f.Equals(normalizedQuery, StringComparison.Ordinal));
    }

    private static VocabularyWorkflowItemResult CloneForInput(
        VocabularyWorkflowItemResult source,
        string input)
    {
        if (source.Input.Equals(input, StringComparison.Ordinal)
            && source.Lookup.Query.Equals(input, StringComparison.Ordinal))
        {
            return source;
        }

        var lookup = source.Lookup.Query.Equals(input, StringComparison.Ordinal)
            ? source.Lookup
            : source.Lookup with { Query = input };

        return source with
        {
            Input = input,
            Lookup = lookup
        };
    }
}
