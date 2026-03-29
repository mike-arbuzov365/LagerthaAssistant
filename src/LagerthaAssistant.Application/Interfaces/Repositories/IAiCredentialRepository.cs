namespace LagerthaAssistant.Application.Interfaces.Repositories;

using LagerthaAssistant.Domain.Entities;

public interface IAiCredentialRepository
{
    Task<UserAiCredential?> GetAsync(
        string channel,
        string userId,
        string provider,
        CancellationToken cancellationToken = default);

    Task AddAsync(UserAiCredential entry, CancellationToken cancellationToken = default);

    Task RemoveAsync(UserAiCredential entry, CancellationToken cancellationToken = default);
}
