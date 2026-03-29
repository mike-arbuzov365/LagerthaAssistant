namespace BaguetteDesign.Infrastructure.Repositories;

using BaguetteDesign.Application.Interfaces;
using BaguetteDesign.Domain.Entities;
using BaguetteDesign.Infrastructure.Data;

public sealed class LeadRepository : ILeadRepository
{
    private readonly BaguetteDbContext _db;

    public LeadRepository(BaguetteDbContext db)
    {
        _db = db;
    }

    public Task AddAsync(Lead lead, CancellationToken cancellationToken = default)
    {
        _db.Leads.Add(lead);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _db.SaveChangesAsync(cancellationToken);
}
