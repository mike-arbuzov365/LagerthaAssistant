namespace LagerthaAssistant.Application.Services.Vocabulary;

using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Vocabulary;
using Microsoft.Extensions.Logging;

public sealed class VocabularyPersistenceService : IVocabularyPersistenceService
{
    private readonly IVocabularyDeckService _deckService;
    private readonly IVocabularyIndexService _indexService;
    private readonly IVocabularyStorageModeProvider _storageModeProvider;
    private readonly ILogger<VocabularyPersistenceService> _logger;

    public VocabularyPersistenceService(
        IVocabularyDeckService deckService,
        IVocabularyIndexService indexService,
        IVocabularyStorageModeProvider storageModeProvider,
        ILogger<VocabularyPersistenceService> logger)
    {
        _deckService = deckService;
        _indexService = indexService;
        _storageModeProvider = storageModeProvider;
        _logger = logger;
    }

    public async Task<VocabularyAppendResult> AppendFromAssistantReplyAsync(
        string requestedWord,
        string assistantReply,
        string? forcedDeckFileName = null,
        string? overridePartOfSpeech = null,
        CancellationToken cancellationToken = default)
    {
        var mode = _storageModeProvider.CurrentMode;

        var result = await _deckService.AppendFromAssistantReplyAsync(
            requestedWord,
            assistantReply,
            forcedDeckFileName,
            overridePartOfSpeech,
            cancellationToken);

        try
        {
            await _indexService.HandleAppendResultAsync(
                requestedWord,
                assistantReply,
                forcedDeckFileName,
                overridePartOfSpeech,
                result,
                mode,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update vocabulary SQL index after append operation.");
        }

        return result;
    }
}
