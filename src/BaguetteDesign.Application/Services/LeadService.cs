namespace BaguetteDesign.Application.Services;

using BaguetteDesign.Application.Interfaces;
using BaguetteDesign.Domain.Entities;
using BaguetteDesign.Domain.Enums;

public sealed class LeadService : ILeadService
{
    private readonly ILeadRepository _leads;

    public LeadService(ILeadRepository leads) => _leads = leads;

    public Task<IReadOnlyList<Lead>> GetLeadsAsync(LeadStatus? status = null, CancellationToken ct = default)
        => _leads.GetAllAsync(status, ct);

    public Task<Lead?> GetByIdAsync(int leadId, CancellationToken ct = default)
        => _leads.GetByIdAsync(leadId, ct);

    public async Task ChangeStatusAsync(int leadId, LeadStatus newStatus, CancellationToken ct = default)
    {
        var lead = await _leads.GetByIdAsync(leadId, ct);
        if (lead is null) return;

        lead.Status = newStatus;
        await _leads.SaveChangesAsync(ct);
    }
}
