namespace LagerthaAssistant.Application.Interfaces.Repositories;

using LagerthaAssistant.Domain.Entities;

public interface IVocabularyCardRepository
{
    Task<IReadOnlyList<VocabularyCard>> FindByAnyTokenAsync(
        IReadOnlyCollection<string> normalizedTokens,
        CancellationToken cancellationToken = default);

    Task<VocabularyCard?> GetByIdentityAsync(
        string normalizedWord,
        string deckFileName,
        string storageMode,
        CancellationToken cancellationToken = default);

    Task AddAsync(VocabularyCard card, CancellationToken cancellationToken = default);
}
