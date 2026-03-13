namespace LagerthaAssistant.Application.Interfaces.Repositories;

using LagerthaAssistant.Domain.Entities;

public interface IVocabularySyncJobRepository
{
    Task AddAsync(VocabularySyncJob job, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VocabularySyncJob>> GetPendingAsync(int take, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VocabularySyncJob>> ClaimPendingAsync(
        int take,
        DateTimeOffset claimedAtUtc,
        CancellationToken cancellationToken = default);

    Task<VocabularySyncJob?> FindActiveDuplicateAsync(
        string requestedWord,
        string assistantReply,
        string targetDeckFileName,
        string storageMode,
        string? overridePartOfSpeech,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VocabularySyncJob>> GetFailedAsync(int take, CancellationToken cancellationToken = default);

    Task<int> RequeueFailedAsync(
        int take,
        DateTimeOffset requeuedAtUtc,
        CancellationToken cancellationToken = default);

    Task<int> CountPendingAsync(CancellationToken cancellationToken = default);
}
