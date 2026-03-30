namespace BaguetteDesign.Infrastructure.Repositories;

using BaguetteDesign.Application.Interfaces;
using BaguetteDesign.Domain.Entities;
using BaguetteDesign.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

public sealed class DialogStateRepository : IDialogStateRepository
{
    private readonly BaguetteDbContext _db;

    public DialogStateRepository(BaguetteDbContext db) => _db = db;

    public async Task<IReadOnlyList<DialogState>> GetAllAsync(CancellationToken ct = default)
        => await _db.DialogStates
            .OrderByDescending(d => d.LastClientMessageAt)
            .ToListAsync(ct);

    public Task<DialogState?> GetByClientUserIdAsync(string clientUserId, CancellationToken ct = default)
        => _db.DialogStates.FirstOrDefaultAsync(d => d.ClientUserId == clientUserId, ct);

    public async Task UpsertAsync(DialogState state, CancellationToken ct = default)
    {
        var existing = await GetByClientUserIdAsync(state.ClientUserId, ct);
        if (existing is null)
        {
            _db.DialogStates.Add(state);
        }
        else
        {
            existing.Status = state.Status;
            existing.LastClientMessagePreview = state.LastClientMessagePreview ?? existing.LastClientMessagePreview;
            existing.LastClientMessageAt = state.LastClientMessageAt ?? existing.LastClientMessageAt;
        }
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
