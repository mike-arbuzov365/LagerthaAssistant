namespace BaguetteDesign.Infrastructure.Repositories;

using BaguetteDesign.Application.Interfaces;
using BaguetteDesign.Domain.Entities;
using BaguetteDesign.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

public sealed class ClientFileRepository : IClientFileRepository
{
    private readonly BaguetteDbContext _db;
    public ClientFileRepository(BaguetteDbContext db) => _db = db;

    public Task AddAsync(ClientFile file, CancellationToken ct = default)
    {
        _db.ClientFiles.Add(file);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<ClientFile>> GetByClientUserIdAsync(string clientUserId, CancellationToken ct = default)
        => await _db.ClientFiles
            .Where(f => f.ClientUserId == clientUserId)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
