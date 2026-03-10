namespace LagerthaAssistant.Application.Interfaces.Repositories;

using LagerthaAssistant.Domain.Entities;

public interface ISystemPromptRepository
{
    Task<SystemPromptEntry?> GetActiveAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SystemPromptEntry>> GetRecentAsync(int take, CancellationToken cancellationToken = default);

    Task<int> GetLatestVersionAsync(CancellationToken cancellationToken = default);

    Task AddAsync(SystemPromptEntry entry, CancellationToken cancellationToken = default);
}
