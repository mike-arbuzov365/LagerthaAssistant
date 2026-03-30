namespace BaguetteDesign.Infrastructure.Repositories;

using BaguetteDesign.Application.Interfaces;
using BaguetteDesign.Domain.Entities;
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

    public Task<Lead?> GetLatestByUserIdAsync(string userId, CancellationToken cancellationToken = default)
        => _db.Leads
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _db.SaveChangesAsync(cancellationToken);
}
