namespace LagerthaAssistant.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using LagerthaAssistant.Application.Interfaces.Repositories;
using LagerthaAssistant.Domain.Entities;
using LagerthaAssistant.Infrastructure.Constants;
using LagerthaAssistant.Infrastructure.Data;
using LagerthaAssistant.Infrastructure.Exceptions;

public sealed class SystemPromptRepository : ISystemPromptRepository
{
    private readonly AppDbContext _context;
    private readonly ILogger<SystemPromptRepository> _logger;

    public SystemPromptRepository(AppDbContext context, ILogger<SystemPromptRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<SystemPromptEntry?> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Executing {Operation} for active system prompt", RepositoryOperations.GetActive);

            return await _context.SystemPromptEntries
                .FirstOrDefaultAsync(x => x.IsActive, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for active system prompt", RepositoryOperations.GetActive);
            throw new RepositoryException(nameof(SystemPromptRepository), RepositoryOperations.GetActive, "Failed to load active system prompt", ex);
        }
    }

    public async Task<IReadOnlyList<SystemPromptEntry>> GetRecentAsync(int take, CancellationToken cancellationToken = default)
    {
        if (take <= 0)
        {
            return [];
        }

        try
        {
            _logger.LogDebug("Executing {Operation} for system prompt history; Take: {Take}", RepositoryOperations.GetRecent, take);

            return await _context.SystemPromptEntries
                .AsNoTracking()
                .OrderByDescending(x => x.Version)
                .Take(take)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for system prompt history", RepositoryOperations.GetRecent);
            throw new RepositoryException(nameof(SystemPromptRepository), RepositoryOperations.GetRecent, "Failed to load system prompt history", ex);
        }
    }

    public async Task<int> GetLatestVersionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Executing {Operation} for latest system prompt version", RepositoryOperations.GetLatest);

            var latest = await _context.SystemPromptEntries
                .AsNoTracking()
                .OrderByDescending(x => x.Version)
                .Select(x => (int?)x.Version)
                .FirstOrDefaultAsync(cancellationToken);

            return latest ?? 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for latest system prompt version", RepositoryOperations.GetLatest);
            throw new RepositoryException(nameof(SystemPromptRepository), RepositoryOperations.GetLatest, "Failed to load latest prompt version", ex);
        }
    }

    public Task AddAsync(SystemPromptEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        try
        {
            _logger.LogDebug("Executing {Operation} for system prompt version {Version}", RepositoryOperations.Add, entry.Version);
            _context.SystemPromptEntries.Add(entry);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for system prompt version {Version}", RepositoryOperations.Add, entry.Version);
            throw new RepositoryException(nameof(SystemPromptRepository), RepositoryOperations.Add, "Failed to add system prompt", ex);
        }
    }
}
