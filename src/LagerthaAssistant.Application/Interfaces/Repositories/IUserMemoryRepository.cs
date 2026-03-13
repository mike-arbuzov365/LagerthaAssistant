namespace LagerthaAssistant.Application.Interfaces.Repositories;

using LagerthaAssistant.Domain.Entities;

public interface IUserMemoryRepository
{
    Task<UserMemoryEntry?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);

    Task<UserMemoryEntry?> GetByKeyAsync(
        string key,
        string channel,
        string userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserMemoryEntry>> GetActiveAsync(int take, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserMemoryEntry>> GetActiveAsync(
        int take,
        string channel,
        string userId,
        CancellationToken cancellationToken = default);

    Task AddAsync(UserMemoryEntry entry, CancellationToken cancellationToken = default);
}
