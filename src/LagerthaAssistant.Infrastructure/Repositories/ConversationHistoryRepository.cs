namespace LagerthaAssistant.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using LagerthaAssistant.Application.Interfaces.Repositories;
using LagerthaAssistant.Domain.Entities;
using LagerthaAssistant.Infrastructure.Constants;
using LagerthaAssistant.Infrastructure.Data;
using LagerthaAssistant.Infrastructure.Exceptions;

public sealed class ConversationHistoryRepository : IConversationHistoryRepository
{
    private readonly AppDbContext _context;
    private readonly ILogger<ConversationHistoryRepository> _logger;

    public ConversationHistoryRepository(AppDbContext context, ILogger<ConversationHistoryRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public Task AddAsync(ConversationHistoryEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        try
        {
            _logger.LogDebug("Executing {Operation} for role {Role}", RepositoryOperations.Add, entry.Role);
            _context.ConversationHistoryEntries.Add(entry);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for role {Role}", RepositoryOperations.Add, entry.Role);
            throw new RepositoryException(nameof(ConversationHistoryRepository), RepositoryOperations.Add, "Failed to add history entry", ex);
        }
    }

    public async Task<IReadOnlyList<ConversationHistoryEntry>> GetRecentBySessionIdAsync(
        int sessionId,
        int take,
        CancellationToken cancellationToken = default)
    {
        if (take <= 0)
        {
            return [];
        }

        try
        {
            _logger.LogDebug("Executing {Operation} for SessionId {SessionId}; Take: {Take}", RepositoryOperations.GetRecent, sessionId, take);

            var items = await _context.ConversationHistoryEntries
                .AsNoTracking()
                .Where(x => x.ConversationSessionId == sessionId)
                .OrderByDescending(x => x.SentAtUtc)
                .ThenByDescending(x => x.Id)
                .Take(take)
                .ToListAsync(cancellationToken);

            items.Reverse();
            return items;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for SessionId {SessionId}", RepositoryOperations.GetRecent, sessionId);
            throw new RepositoryException(nameof(ConversationHistoryRepository), RepositoryOperations.GetRecent, "Failed to load recent history", ex);
        }
    }
}
