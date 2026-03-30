namespace BaguetteDesign.Application.Interfaces;

using BaguetteDesign.Domain.Entities;

public interface IClientFileRepository
{
    Task AddAsync(ClientFile file, CancellationToken ct = default);
    Task<IReadOnlyList<ClientFile>> GetByClientUserIdAsync(string clientUserId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
