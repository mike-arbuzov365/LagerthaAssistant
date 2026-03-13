namespace LagerthaAssistant.Infrastructure.Services.Vocabulary;

using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Vocabulary;

public sealed class VocabularyDeckModeService : IVocabularyDeckModeService
{
    private readonly IVocabularyDeckBackendResolver _backendResolver;

    public VocabularyDeckModeService(
        IVocabularyDeckBackendResolver backendResolver)
    {
        _backendResolver = backendResolver;
    }

    public Task<VocabularyAppendResult> AppendFromAssistantReplyAsync(
        VocabularyStorageMode mode,
        string requestedWord,
        string assistantReply,
        string? forcedDeckFileName = null,
        string? overridePartOfSpeech = null,
        CancellationToken cancellationToken = default)
    {
        var backend = _backendResolver.Resolve(mode);
        return backend.AppendFromAssistantReplyAsync(
            requestedWord,
            assistantReply,
            forcedDeckFileName,
            overridePartOfSpeech,
            cancellationToken);
    }
}
