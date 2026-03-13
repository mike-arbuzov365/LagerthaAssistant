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

    public VocabularyWorkflowService(
        IAssistantSessionService assistantSessionService,
        IVocabularyDeckService vocabularyDeckService,
        IVocabularyIndexService? vocabularyIndexService = null,
        IVocabularyStorageModeProvider? storageModeProvider = null)
    {
        _assistantSessionService = assistantSessionService;
        _vocabularyDeckService = vocabularyDeckService;
        _vocabularyIndexService = vocabularyIndexService;
        _storageModeProvider = storageModeProvider;
    }

    public async Task<VocabularyWorkflowItemResult> ProcessAsync(
        string input,
        string? forcedDeckFileName = null,
        string? overridePartOfSpeech = null,
        CancellationToken cancellationToken = default)
    {
        return await ProcessCoreAsync(
            input,
            forcedDeckFileName,
            overridePartOfSpeech,
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

        var indexedLookups = _vocabularyIndexService is null
            ? null
            : await _vocabularyIndexService.FindByInputsAsync(normalizedInputs, cancellationToken);

        var results = new List<VocabularyWorkflowItemResult>(normalizedInputs.Count);

        foreach (var input in normalizedInputs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (indexedLookups is not null
                && indexedLookups.TryGetValue(input, out var indexedLookup)
                && indexedLookup.Found)
            {
                results.Add(new VocabularyWorkflowItemResult(input, indexedLookup));
                continue;
            }

            var result = await ProcessCoreAsync(
                input,
                forcedDeckFileName: null,
                overridePartOfSpeech: null,
                skipIndexLookup: indexedLookups is not null,
                cancellationToken);
            results.Add(result);
        }

        return results;
    }

    private async Task<VocabularyWorkflowItemResult> ProcessCoreAsync(
        string input,
        string? forcedDeckFileName,
        string? overridePartOfSpeech,
        bool skipIndexLookup,
        CancellationToken cancellationToken = default)
    {
        var normalizedInput = input?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedInput))
        {
            throw new ArgumentException("Input cannot be empty.", nameof(input));
        }

        if (!skipIndexLookup && _vocabularyIndexService is not null)
        {
            var indexedLookup = await _vocabularyIndexService.FindByInputAsync(normalizedInput, cancellationToken);
            if (indexedLookup.Found)
            {
                return new VocabularyWorkflowItemResult(normalizedInput, indexedLookup);
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
}
