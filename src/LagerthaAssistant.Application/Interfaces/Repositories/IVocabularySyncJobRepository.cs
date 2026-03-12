namespace LagerthaAssistant.Application.Interfaces.Repositories;

using LagerthaAssistant.Domain.Entities;

public interface IVocabularySyncJobRepository
{
    Task AddAsync(VocabularySyncJob job, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VocabularySyncJob>> GetPendingAsync(int take, CancellationToken cancellationToken = default);
}
