namespace BaguetteDesign.Application.Interfaces;

using BaguetteDesign.Domain.Entities;
using BaguetteDesign.Domain.Enums;

public interface ILeadRepository
{
    Task AddAsync(Lead lead, CancellationToken cancellationToken = default);
    Task<Lead?> GetByIdAsync(int leadId, CancellationToken cancellationToken = default);
    Task<Lead?> GetLatestByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Lead>> GetAllAsync(LeadStatus? status = null, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
