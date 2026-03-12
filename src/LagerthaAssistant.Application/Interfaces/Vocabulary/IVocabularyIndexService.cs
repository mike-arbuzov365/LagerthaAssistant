namespace LagerthaAssistant.Application.Interfaces.Vocabulary;

using LagerthaAssistant.Application.Models.Vocabulary;

public interface IVocabularyIndexService
{
    Task<VocabularyLookupResult> FindByInputAsync(string input, CancellationToken cancellationToken = default);

    Task IndexLookupResultAsync(
        VocabularyLookupResult lookup,
        VocabularyStorageMode storageMode,
        CancellationToken cancellationToken = default);

    Task HandleAppendResultAsync(
        string requestedWord,
        string assistantReply,
        string? targetDeckFileName,
        string? overridePartOfSpeech,
        VocabularyAppendResult appendResult,
        VocabularyStorageMode storageMode,
        CancellationToken cancellationToken = default);
}
