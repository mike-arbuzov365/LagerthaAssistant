namespace LagerthaAssistant.Infrastructure.Services.Vocabulary;

using LagerthaAssistant.Application.Models.Vocabulary;

public interface IVocabularyBatchDeckLookupBackend
{
    Task<IReadOnlyDictionary<string, VocabularyLookupResult>> FindInWritableDecksBatchAsync(
        IReadOnlyList<string> words,
        CancellationToken cancellationToken = default);
}
