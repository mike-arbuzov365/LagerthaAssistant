namespace LagerthaAssistant.Infrastructure.Services.Vocabulary;

using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Vocabulary;

public sealed class SwitchableVocabularyDeckService : IVocabularyDeckService
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
