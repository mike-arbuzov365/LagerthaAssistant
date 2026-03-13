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

    public async Task<IReadOnlyList<VocabularySyncJob>> ClaimPendingAsync(
        int take,
        DateTimeOffset claimedAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (take <= 0)
        {
            return [];
        }

        try
        {
            _logger.LogDebug("Executing {Operation}; Take: {Take}; ClaimedAtUtc: {ClaimedAtUtc}", RepositoryOperations.GetActive, take, claimedAtUtc);

            var probeTake = Math.Clamp(take * 3, take, 2000);
            var candidateIds = await _context.VocabularySyncJobs
                .AsNoTracking()
                .Where(x => x.Status == VocabularySyncJobStatus.Pending)
                .OrderBy(x => x.CreatedAtUtc)
                .ThenBy(x => x.Id)
                .Select(x => x.Id)
                .Take(probeTake)
                .ToListAsync(cancellationToken);

            if (candidateIds.Count == 0)
            {
                return [];
            }

            var claimed = new List<VocabularySyncJob>(Math.Min(take, candidateIds.Count));
            foreach (var id in candidateIds)
            {
                if (claimed.Count >= take)
                {
                    break;
                }

                var updated = await _context.VocabularySyncJobs
                    .Where(x => x.Id == id && x.Status == VocabularySyncJobStatus.Pending)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(x => x.Status, VocabularySyncJobStatus.Processing)
                            .SetProperty(x => x.AttemptCount, x => x.AttemptCount + 1)
                            .SetProperty(x => x.LastAttemptAtUtc, claimedAtUtc),
                        cancellationToken);

                if (updated == 0)
                {
                    continue;
                }

                var job = await _context.VocabularySyncJobs
                    .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

                if (job is not null)
                {
                    claimed.Add(job);
                }
            }

            return claimed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for claiming pending vocabulary sync jobs", RepositoryOperations.GetActive);
            throw new RepositoryException(nameof(VocabularySyncJobRepository), RepositoryOperations.GetActive, "Failed to claim pending vocabulary sync jobs", ex);
        }
    }

    public async Task<VocabularySyncJob?> FindActiveDuplicateAsync(
        string requestedWord,
        string assistantReply,
        string targetDeckFileName,
        string storageMode,
        string? overridePartOfSpeech,
        CancellationToken cancellationToken = default)
    {
        var normalizedRequestedWord = requestedWord?.Trim() ?? string.Empty;
        var normalizedAssistantReply = assistantReply?.Trim() ?? string.Empty;
        var normalizedTargetDeck = targetDeckFileName?.Trim() ?? string.Empty;
        var normalizedStorageMode = storageMode?.Trim() ?? string.Empty;
        var normalizedOverridePartOfSpeech = string.IsNullOrWhiteSpace(overridePartOfSpeech)
            ? null
            : overridePartOfSpeech.Trim();

        if (string.IsNullOrWhiteSpace(normalizedRequestedWord)
            || string.IsNullOrWhiteSpace(normalizedAssistantReply)
            || string.IsNullOrWhiteSpace(normalizedTargetDeck)
            || string.IsNullOrWhiteSpace(normalizedStorageMode))
        {
            return null;
        }

        try
        {
            _logger.LogDebug(
                "Executing {Operation}; Word: {Word}; Deck: {Deck}; Mode: {Mode}",
                RepositoryOperations.GetByKey,
                normalizedRequestedWord,
                normalizedTargetDeck,
                normalizedStorageMode);

            return await _context.VocabularySyncJobs
                .FirstOrDefaultAsync(
                    x => (x.Status == VocabularySyncJobStatus.Pending || x.Status == VocabularySyncJobStatus.Processing)
                        && x.RequestedWord == normalizedRequestedWord
                        && x.AssistantReply == normalizedAssistantReply
                        && x.TargetDeckFileName == normalizedTargetDeck
                        && x.StorageMode == normalizedStorageMode
                        && x.OverridePartOfSpeech == normalizedOverridePartOfSpeech,
                    cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for active duplicate vocabulary sync job lookup", RepositoryOperations.GetByKey);
            throw new RepositoryException(nameof(VocabularySyncJobRepository), RepositoryOperations.GetByKey, "Failed to lookup active duplicate vocabulary sync job", ex);
        }
    }

    public async Task<IReadOnlyList<VocabularySyncJob>> GetFailedAsync(int take, CancellationToken cancellationToken = default)
    {
        if (take <= 0)
        {
            return [];
        }

        try
        {
            _logger.LogDebug("Executing {Operation}; Take: {Take}; Status: {Status}", RepositoryOperations.GetActive, take, VocabularySyncJobStatus.Failed);

            return await _context.VocabularySyncJobs
                .Where(x => x.Status == VocabularySyncJobStatus.Failed)
                .OrderByDescending(x => x.LastAttemptAtUtc ?? x.CreatedAtUtc)
                .ThenByDescending(x => x.Id)
                .Take(take)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for failed vocabulary sync jobs", RepositoryOperations.GetActive);
            throw new RepositoryException(nameof(VocabularySyncJobRepository), RepositoryOperations.GetActive, "Failed to load failed vocabulary sync jobs", ex);
        }
    }

    public async Task<int> RequeueFailedAsync(
        int take,
        DateTimeOffset requeuedAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (take <= 0)
        {
            return 0;
        }

        try
        {
            _logger.LogDebug("Executing {Operation}; Take: {Take}; RequeuedAtUtc: {RequeuedAtUtc}", RepositoryOperations.Update, take, requeuedAtUtc);

            var candidateIds = await _context.VocabularySyncJobs
                .AsNoTracking()
                .Where(x => x.Status == VocabularySyncJobStatus.Failed)
                .OrderByDescending(x => x.LastAttemptAtUtc ?? x.CreatedAtUtc)
                .ThenByDescending(x => x.Id)
                .Select(x => x.Id)
                .Take(take)
                .ToListAsync(cancellationToken);

            if (candidateIds.Count == 0)
            {
                return 0;
            }

            var updated = await _context.VocabularySyncJobs
                .Where(x => candidateIds.Contains(x.Id) && x.Status == VocabularySyncJobStatus.Failed)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(x => x.Status, VocabularySyncJobStatus.Pending)
                        .SetProperty(x => x.AttemptCount, 0)
                        .SetProperty(x => x.LastError, (string?)null)
                        .SetProperty(x => x.LastAttemptAtUtc, (DateTimeOffset?)null)
                        .SetProperty(x => x.CompletedAtUtc, (DateTimeOffset?)null),
                    cancellationToken);

            return updated;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for requeueing failed vocabulary sync jobs", RepositoryOperations.Update);
            throw new RepositoryException(nameof(VocabularySyncJobRepository), RepositoryOperations.Update, "Failed to requeue failed vocabulary sync jobs", ex);
        }
    }

    public async Task<int> CountPendingAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Executing {Operation} for pending vocabulary sync jobs count", RepositoryOperations.GetActive);

            return await _context.VocabularySyncJobs
                .Where(x => x.Status == VocabularySyncJobStatus.Pending)
                .CountAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for pending vocabulary sync jobs count", RepositoryOperations.GetActive);
            throw new RepositoryException(nameof(VocabularySyncJobRepository), RepositoryOperations.GetActive, "Failed to count pending vocabulary sync jobs", ex);
        }
    }
}
