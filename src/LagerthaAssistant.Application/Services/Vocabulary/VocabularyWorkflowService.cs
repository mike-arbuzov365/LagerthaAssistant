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
        var normalizedInput = input?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedInput))
        {
            throw new ArgumentException("Input cannot be empty.", nameof(input));
        }

        if (_vocabularyIndexService is not null)
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

    public async Task<IReadOnlyList<VocabularyWorkflowItemResult>> ProcessBatchAsync(
        IReadOnlyList<string> inputs,
        CancellationToken cancellationToken = default)
    {
        if (inputs is null)
        {
            throw new ArgumentNullException(nameof(inputs));
        }

        var results = new List<VocabularyWorkflowItemResult>(inputs.Count);

        foreach (var input in inputs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(input))
            {
                continue;
            }

            var result = await ProcessAsync(input, cancellationToken: cancellationToken);
            results.Add(result);
        }

        return results;
    }
}
