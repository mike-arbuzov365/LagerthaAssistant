namespace BaguetteDesign.Application.Interfaces;

using BaguetteDesign.Domain.Entities;
using BaguetteDesign.Domain.Enums;

public interface ILeadService
{
    Task<IReadOnlyList<Lead>> GetLeadsAsync(LeadStatus? status = null, CancellationToken ct = default);
    Task ChangeStatusAsync(int leadId, LeadStatus newStatus, CancellationToken ct = default);
    Task<Lead?> GetByIdAsync(int leadId, CancellationToken ct = default);
}
