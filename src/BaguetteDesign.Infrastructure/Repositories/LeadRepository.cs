namespace BaguetteDesign.Infrastructure.Repositories;

using BaguetteDesign.Application.Interfaces;
using BaguetteDesign.Domain.Entities;
using BaguetteDesign.Domain.Enums;
using BaguetteDesign.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

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

    public Task<Lead?> GetByIdAsync(int leadId, CancellationToken cancellationToken = default)
        => _db.Leads.FirstOrDefaultAsync(l => l.Id == leadId, cancellationToken);

    public Task<Lead?> GetLatestByUserIdAsync(string userId, CancellationToken cancellationToken = default)
        => _db.Leads
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<Lead>> GetAllAsync(LeadStatus? status = null, CancellationToken cancellationToken = default)
    {
        var query = _db.Leads.AsQueryable();
        if (status.HasValue)
            query = query.Where(l => l.Status == status.Value);
        return await query.OrderByDescending(l => l.CreatedAt).ToListAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _db.SaveChangesAsync(cancellationToken);
}
