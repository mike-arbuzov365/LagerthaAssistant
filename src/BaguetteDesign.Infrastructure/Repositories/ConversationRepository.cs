namespace BaguetteDesign.Infrastructure.Repositories;

using BaguetteDesign.Application.Interfaces;
using BaguetteDesign.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using SharedBotKernel.Domain.Entities;

public sealed class ConversationRepository : IConversationRepository
{
    private const string TelegramChannel = "telegram";

    private readonly BaguetteDbContext _db;

    public ConversationRepository(BaguetteDbContext db)
    {
        _db = db;
    }

    public async Task<ConversationSession> FindOrCreateSessionAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var session = await _db.ConversationSessions
            .FirstOrDefaultAsync(
                s => s.UserId == userId && s.Channel == TelegramChannel,
                cancellationToken);

        if (session is not null)
            return session;

        session = ConversationSession.Create(
            sessionKey: Guid.NewGuid(),
            title: null,
            channel: TelegramChannel,
            userId: userId);

        _db.ConversationSessions.Add(session);
        await _db.SaveChangesAsync(cancellationToken);

        return session;
    }

    public async Task<IReadOnlyList<ConversationHistoryEntry>> GetRecentHistoryAsync(
        int sessionId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        return await _db.ConversationHistoryEntries
            .Where(e => e.ConversationSessionId == sessionId)
            .OrderByDescending(e => e.SentAtUtc)
            .Take(limit)
            .OrderBy(e => e.SentAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task AddEntryAsync(
        ConversationHistoryEntry entry,
        CancellationToken cancellationToken = default)
    {
        _db.ConversationHistoryEntries.Add(entry);
        await Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _db.SaveChangesAsync(cancellationToken);
}
