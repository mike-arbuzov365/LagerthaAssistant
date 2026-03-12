namespace LagerthaAssistant.Infrastructure.Repositories;

using LagerthaAssistant.Application.Interfaces.Repositories;
using LagerthaAssistant.Domain.Entities;
using LagerthaAssistant.Domain.Enums;
using LagerthaAssistant.Infrastructure.Constants;
using LagerthaAssistant.Infrastructure.Data;
using LagerthaAssistant.Infrastructure.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public sealed class VocabularySyncJobRepository : IVocabularySyncJobRepository
{
    private readonly AppDbContext _context;
    private readonly ILogger<VocabularySyncJobRepository> _logger;

    public VocabularySyncJobRepository(AppDbContext context, ILogger<VocabularySyncJobRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public Task AddAsync(VocabularySyncJob job, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);

        try
        {
            _logger.LogDebug("Executing {Operation} for sync job {Word}", RepositoryOperations.Add, job.RequestedWord);
            _context.VocabularySyncJobs.Add(job);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for sync job {Word}", RepositoryOperations.Add, job.RequestedWord);
            throw new RepositoryException(nameof(VocabularySyncJobRepository), RepositoryOperations.Add, "Failed to add vocabulary sync job", ex);
        }
    }

    public async Task<IReadOnlyList<VocabularySyncJob>> GetPendingAsync(int take, CancellationToken cancellationToken = default)
    {
        if (take <= 0)
        {
            return [];
        }

        try
        {
            _logger.LogDebug("Executing {Operation}; Take: {Take}", RepositoryOperations.GetActive, take);

            return await _context.VocabularySyncJobs
                .Where(x => x.Status == VocabularySyncJobStatus.Pending)
                .OrderBy(x => x.CreatedAtUtc)
                .Take(take)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for pending vocabulary sync jobs", RepositoryOperations.GetActive);
            throw new RepositoryException(nameof(VocabularySyncJobRepository), RepositoryOperations.GetActive, "Failed to load pending vocabulary sync jobs", ex);
        }
    }
}
