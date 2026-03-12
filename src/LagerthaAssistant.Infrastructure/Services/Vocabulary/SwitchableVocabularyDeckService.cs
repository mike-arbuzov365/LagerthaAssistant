namespace LagerthaAssistant.Infrastructure.Services.Vocabulary;

using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Vocabulary;
using Microsoft.Extensions.Logging;

public sealed class SwitchableVocabularyDeckService : IVocabularyDeckService
{
    private readonly IReadOnlyDictionary<VocabularyStorageMode, IVocabularyDeckBackend> _backends;
    private readonly IVocabularyStorageModeProvider _modeProvider;
    private readonly ILogger<SwitchableVocabularyDeckService> _logger;

    public SwitchableVocabularyDeckService(
        IEnumerable<IVocabularyDeckBackend> backends,
        IVocabularyStorageModeProvider modeProvider,
        ILogger<SwitchableVocabularyDeckService> logger)
    {
        _backends = backends.ToDictionary(x => x.Mode);
        _modeProvider = modeProvider;
        _logger = logger;
    }

    public Task<VocabularyLookupResult> FindInWritableDecksAsync(string word, CancellationToken cancellationToken = default)
    {
        return GetBackend().FindInWritableDecksAsync(word, cancellationToken);
    }

    public Task<IReadOnlyList<VocabularyDeckFile>> GetWritableDeckFilesAsync(CancellationToken cancellationToken = default)
    {
        return GetBackend().GetWritableDeckFilesAsync(cancellationToken);
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
        var mode = _modeProvider.CurrentMode;

        if (_backends.TryGetValue(mode, out var backend))
        {
            return backend;
        }

        _logger.LogWarning("Vocabulary backend for mode {Mode} is not registered. Falling back to local.", mode);

        if (_backends.TryGetValue(VocabularyStorageMode.Local, out var localBackend))
        {
            return localBackend;
        }

        throw new InvalidOperationException("No vocabulary backends are registered.");
    }
}
