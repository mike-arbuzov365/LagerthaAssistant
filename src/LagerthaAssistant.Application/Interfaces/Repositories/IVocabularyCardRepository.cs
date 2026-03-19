namespace LagerthaAssistant.Application.Interfaces.Repositories;

using LagerthaAssistant.Application.Models.Vocabulary;
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

    Task<int> CountPendingNotionSyncAsync(CancellationToken cancellationToken = default);

    Task<int> CountFailedNotionSyncAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VocabularyCard>> ClaimPendingNotionSyncAsync(
        int take,
        DateTimeOffset claimedAtUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VocabularyCard>> GetFailedNotionSyncAsync(
        int take,
        CancellationToken cancellationToken = default);

    Task<int> RequeueFailedNotionSyncAsync(
        int take,
        DateTimeOffset requeuedAtUtc,
        CancellationToken cancellationToken = default);

    Task<int> CountAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VocabularyCard>> GetRecentAsync(
        int take,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VocabularyDeckStat>> GetDeckStatsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VocabularyPartOfSpeechStat>> GetPartOfSpeechStatsAsync(
        CancellationToken cancellationToken = default);

    Task<int> DeleteAllAsync(CancellationToken cancellationToken = default);
}
