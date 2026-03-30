namespace BaguetteDesign.Infrastructure.Repositories;

using BaguetteDesign.Application.Interfaces;
using BaguetteDesign.Domain.Entities;
using BaguetteDesign.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

public sealed class ProjectRepository : IProjectRepository
{
    private readonly BaguetteDbContext _db;
    public ProjectRepository(BaguetteDbContext db) => _db = db;

    public Task AddAsync(Project project, CancellationToken ct = default)
    {
        _db.Projects.Add(project);
        return Task.CompletedTask;
    }

    public Task<Project?> GetByIdAsync(int projectId, CancellationToken ct = default)
        => _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct);

    public async Task<IReadOnlyList<Project>> GetAllAsync(CancellationToken ct = default)
        => await _db.Projects.OrderByDescending(p => p.CreatedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<Project>> GetByClientUserIdAsync(string clientUserId, CancellationToken ct = default)
        => await _db.Projects
            .Where(p => p.ClientUserId == clientUserId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
