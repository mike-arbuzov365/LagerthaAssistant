namespace LagerthaAssistant.Infrastructure.Services.Vocabulary;

using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Vocabulary;
using Microsoft.Extensions.Logging;

public sealed class VocabularyDeckBackendResolver : IVocabularyDeckBackendResolver
{
    private readonly IReadOnlyDictionary<VocabularyStorageMode, IVocabularyDeckBackend> _backends;
    private readonly ILogger<VocabularyDeckBackendResolver> _logger;

    public VocabularyDeckBackendResolver(
        IEnumerable<IVocabularyDeckBackend> backends,
        ILogger<VocabularyDeckBackendResolver> logger)
    {
        _backends = backends.ToDictionary(x => x.Mode);
        _logger = logger;
    }

    public IVocabularyDeckBackend Resolve(VocabularyStorageMode mode)
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
