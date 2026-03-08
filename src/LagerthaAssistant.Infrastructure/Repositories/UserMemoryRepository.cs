namespace LagerthaAssistant.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using LagerthaAssistant.Application.Interfaces.Repositories;
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

    public async Task<UserMemoryEntry?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Executing {Operation} for key {Key}", RepositoryOperations.GetByKey, key);

            return await _context.UserMemoryEntries
                .FirstOrDefaultAsync(x => x.Key == key, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for key {Key}", RepositoryOperations.GetByKey, key);
            throw new RepositoryException(nameof(UserMemoryRepository), RepositoryOperations.GetByKey, "Failed to load memory by key", ex);
        }
    }

    public async Task<IReadOnlyList<UserMemoryEntry>> GetActiveAsync(int take, CancellationToken cancellationToken = default)
    {
        if (take <= 0)
        {
            return [];
        }

        try
        {
            _logger.LogDebug("Executing {Operation}; Take: {Take}", RepositoryOperations.GetActive, take);

            return await _context.UserMemoryEntries
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderByDescending(x => x.UpdatedAt)
                .Take(take)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation}", RepositoryOperations.GetActive);
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

