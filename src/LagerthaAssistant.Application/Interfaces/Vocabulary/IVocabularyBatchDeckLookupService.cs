namespace LagerthaAssistant.Application.Interfaces.Vocabulary;

using LagerthaAssistant.Application.Models.Vocabulary;

public interface IVocabularyBatchDeckLookupService
{
    Task<IReadOnlyDictionary<string, VocabularyLookupResult>> FindInWritableDecksBatchAsync(
        IReadOnlyList<string> words,
        CancellationToken cancellationToken = default);
}
