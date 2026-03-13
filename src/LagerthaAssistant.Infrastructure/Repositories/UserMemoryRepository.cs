namespace LagerthaAssistant.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using LagerthaAssistant.Application.Interfaces.Repositories;
using LagerthaAssistant.Domain.Constants;
using LagerthaAssistant.Domain.Entities;
using LagerthaAssistant.Infrastructure.Constants;
using LagerthaAssistant.Infrastructure.Data;
using LagerthaAssistant.Infrastructure.Exceptions;

public sealed class UserMemoryRepository : IUserMemoryRepository
{
    private readonly AppDbContext _context;
    private readonly ILogger<UserMemoryRepository> _logger;

    public UserMemoryRepository(AppDbContext context, ILogger<UserMemoryRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public Task<UserMemoryEntry?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
        => GetByKeyAsync(key, ConversationScopeDefaults.Channel, ConversationScopeDefaults.UserId, cancellationToken);

    public async Task<UserMemoryEntry?> GetByKeyAsync(
        string key,
        string channel,
        string userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug(
                "Executing {Operation} for key {Key}; Channel={Channel}; UserId={UserId}",
                RepositoryOperations.GetByKey,
                key,
                channel,
                userId);

            return await _context.UserMemoryEntries
                .FirstOrDefaultAsync(x => x.Key == key && x.Channel == channel && x.UserId == userId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error in {Operation} for key {Key}; Channel={Channel}; UserId={UserId}",
                RepositoryOperations.GetByKey,
                key,
                channel,
                userId);
            throw new RepositoryException(nameof(UserMemoryRepository), RepositoryOperations.GetByKey, "Failed to load memory by key", ex);
        }
    }

    public Task<IReadOnlyList<UserMemoryEntry>> GetActiveAsync(int take, CancellationToken cancellationToken = default)
        => GetActiveAsync(take, ConversationScopeDefaults.Channel, ConversationScopeDefaults.UserId, cancellationToken);

    public async Task<IReadOnlyList<UserMemoryEntry>> GetActiveAsync(
        int take,
        string channel,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (take <= 0)
        {
            return [];
        }

        try
        {
            _logger.LogDebug(
                "Executing {Operation}; Take: {Take}; Channel={Channel}; UserId={UserId}",
                RepositoryOperations.GetActive,
                take,
                channel,
                userId);

            return await _context.UserMemoryEntries
                .AsNoTracking()
                .Where(x => x.IsActive && x.Channel == channel && x.UserId == userId)
                .OrderByDescending(x => x.UpdatedAt)
                .Take(take)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error in {Operation}; Channel={Channel}; UserId={UserId}",
                RepositoryOperations.GetActive,
                channel,
                userId);
            throw new RepositoryException(nameof(UserMemoryRepository), RepositoryOperations.GetActive, "Failed to load active memory", ex);
        }
    }

    public Task AddAsync(UserMemoryEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        try
        {
            _logger.LogDebug("Executing {Operation} for key {Key}", RepositoryOperations.Add, entry.Key);
            _context.UserMemoryEntries.Add(entry);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for key {Key}", RepositoryOperations.Add, entry.Key);
            throw new RepositoryException(nameof(UserMemoryRepository), RepositoryOperations.Add, "Failed to add memory entry", ex);
        }
    }
}
