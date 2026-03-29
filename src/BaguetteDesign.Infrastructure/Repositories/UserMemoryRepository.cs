namespace BaguetteDesign.Infrastructure.Repositories;

using BaguetteDesign.Application.Interfaces;
using BaguetteDesign.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using SharedBotKernel.Domain.Constants;
using SharedBotKernel.Domain.Entities;

public sealed class UserMemoryRepository : IUserMemoryRepository
{
    private const string TelegramChannel = "telegram";

    private readonly BaguetteDbContext _db;

    public UserMemoryRepository(BaguetteDbContext db)
    {
        _db = db;
    }

    public async Task<string?> GetAsync(string userId, string key, CancellationToken cancellationToken = default)
    {
        var entry = await FindAsync(userId, key, cancellationToken);
        return entry?.IsActive == true ? entry.Value : null;
    }

    public async Task SetAsync(string userId, string key, string value, CancellationToken cancellationToken = default)
    {
        var entry = await FindAsync(userId, key, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        if (entry is null)
        {
            _db.UserMemoryEntries.Add(new UserMemoryEntry
            {
                UserId = userId,
                Channel = TelegramChannel,
                Key = key,
                Value = value,
                IsActive = true,
                LastSeenAtUtc = now,
                Confidence = 1.0
            });
        }
        else
        {
            entry.Value = value;
            entry.IsActive = true;
            entry.LastSeenAtUtc = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string userId, string key, CancellationToken cancellationToken = default)
    {
        var entry = await FindAsync(userId, key, cancellationToken);
        if (entry is not null)
        {
            entry.IsActive = false;
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    private Task<UserMemoryEntry?> FindAsync(string userId, string key, CancellationToken ct)
        => _db.UserMemoryEntries
            .FirstOrDefaultAsync(e => e.UserId == userId && e.Key == key && e.Channel == TelegramChannel, ct);
}
