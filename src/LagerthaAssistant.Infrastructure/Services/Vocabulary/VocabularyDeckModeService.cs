namespace LagerthaAssistant.Infrastructure.Services.Vocabulary;

using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Vocabulary;
using Microsoft.Extensions.Logging;

public sealed class VocabularyDeckModeService : IVocabularyDeckModeService
{
    private readonly IReadOnlyDictionary<VocabularyStorageMode, IVocabularyDeckBackend> _backends;
    private readonly ILogger<VocabularyDeckModeService> _logger;

    public VocabularyDeckModeService(
        IEnumerable<IVocabularyDeckBackend> backends,
        ILogger<VocabularyDeckModeService> logger)
    {
        _backends = backends.ToDictionary(x => x.Mode);
        _logger = logger;
    }

    public Task<VocabularyAppendResult> AppendFromAssistantReplyAsync(
        VocabularyStorageMode mode,
        string requestedWord,
        string assistantReply,
        string? forcedDeckFileName = null,
        string? overridePartOfSpeech = null,
        CancellationToken cancellationToken = default)
    {
        var backend = ResolveBackend(mode);
        return backend.AppendFromAssistantReplyAsync(
            requestedWord,
            assistantReply,
            forcedDeckFileName,
            overridePartOfSpeech,
            cancellationToken);
    }

    private IVocabularyDeckBackend ResolveBackend(VocabularyStorageMode mode)
    {
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
