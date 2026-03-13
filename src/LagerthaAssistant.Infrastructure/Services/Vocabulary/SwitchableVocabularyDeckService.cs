namespace LagerthaAssistant.Infrastructure.Services.Vocabulary;

using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Vocabulary;

public sealed class SwitchableVocabularyDeckService : IVocabularyDeckService, IVocabularyBatchDeckLookupService
{
    private readonly IVocabularyDeckBackendResolver _backendResolver;
    private readonly IVocabularyStorageModeProvider _modeProvider;

    public SwitchableVocabularyDeckService(
        IVocabularyDeckBackendResolver backendResolver,
        IVocabularyStorageModeProvider modeProvider)
    {
        _backendResolver = backendResolver;
        _modeProvider = modeProvider;
    }

    public Task<VocabularyLookupResult> FindInWritableDecksAsync(string word, CancellationToken cancellationToken = default)
    {
        return GetBackend().FindInWritableDecksAsync(word, cancellationToken);
    }

    public Task<IReadOnlyList<VocabularyDeckFile>> GetWritableDeckFilesAsync(CancellationToken cancellationToken = default)
    {
        return GetBackend().GetWritableDeckFilesAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, VocabularyLookupResult>> FindInWritableDecksBatchAsync(
        IReadOnlyList<string> words,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(words);

        var normalizedWords = words
            .Where(word => !string.IsNullOrWhiteSpace(word))
            .Select(word => word.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedWords.Count == 0)
        {
            return new Dictionary<string, VocabularyLookupResult>(StringComparer.OrdinalIgnoreCase);
        }

        var backend = GetBackend();
        if (backend is IVocabularyBatchDeckLookupBackend batchBackend)
        {
            return await batchBackend.FindInWritableDecksBatchAsync(normalizedWords, cancellationToken);
        }

        var fallback = new Dictionary<string, VocabularyLookupResult>(StringComparer.OrdinalIgnoreCase);
        foreach (var word in normalizedWords)
        {
            cancellationToken.ThrowIfCancellationRequested();
            fallback[word] = await backend.FindInWritableDecksAsync(word, cancellationToken);
        }

        return fallback;
    }

    public Task<VocabularyAppendPreviewResult> PreviewAppendFromAssistantReplyAsync(
        string requestedWord,
        string assistantReply,
        string? forcedDeckFileName = null,
        string? overridePartOfSpeech = null,
        CancellationToken cancellationToken = default)
    {
        return GetBackend().PreviewAppendFromAssistantReplyAsync(
            requestedWord,
            assistantReply,
            forcedDeckFileName,
            overridePartOfSpeech,
            cancellationToken);
    }

    public Task<VocabularyAppendResult> AppendFromAssistantReplyAsync(
        string requestedWord,
        string assistantReply,
        string? forcedDeckFileName = null,
        string? overridePartOfSpeech = null,
        CancellationToken cancellationToken = default)
    {
        return GetBackend().AppendFromAssistantReplyAsync(
            requestedWord,
            assistantReply,
            forcedDeckFileName,
            overridePartOfSpeech,
            cancellationToken);
    }

    private IVocabularyDeckBackend GetBackend()
    {
        return _backendResolver.Resolve(_modeProvider.CurrentMode);
    }
}
