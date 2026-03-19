namespace LagerthaAssistant.Infrastructure.Repositories;

using LagerthaAssistant.Application.Interfaces.Repositories;
using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.Domain.Entities;
using LagerthaAssistant.Domain.Enums;
using LagerthaAssistant.Infrastructure.Constants;
using LagerthaAssistant.Infrastructure.Data;
using LagerthaAssistant.Infrastructure.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public sealed class VocabularyCardRepository : IVocabularyCardRepository
{
    private readonly AppDbContext _context;
    private readonly ILogger<VocabularyCardRepository> _logger;

    public VocabularyCardRepository(AppDbContext context, ILogger<VocabularyCardRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IReadOnlyList<VocabularyCard>> FindByAnyTokenAsync(
        IReadOnlyCollection<string> normalizedTokens,
        CancellationToken cancellationToken = default)
    {
        if (normalizedTokens.Count == 0)
        {
            return [];
        }

        try
        {
            _logger.LogDebug("Executing {Operation}; TokensCount: {TokensCount}", RepositoryOperations.GetRecent, normalizedTokens.Count);

            return await _context.VocabularyCards
                .AsNoTracking()
                .Include(x => x.Tokens)
                .Where(x => x.Tokens.Any(token => normalizedTokens.Contains(token.TokenNormalized)))
                .OrderByDescending(x => x.LastSeenAtUtc)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for vocabulary token lookup", RepositoryOperations.GetRecent);
            throw new RepositoryException(nameof(VocabularyCardRepository), RepositoryOperations.GetRecent, "Failed to lookup vocabulary cards by token", ex);
        }
    }

    public async Task<VocabularyCard?> GetByIdentityAsync(
        string normalizedWord,
        string deckFileName,
        string storageMode,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug(
                "Executing {Operation}; Word: {Word}; Deck: {Deck}; Mode: {Mode}",
                RepositoryOperations.GetByKey,
                normalizedWord,
                deckFileName,
                storageMode);

            return await _context.VocabularyCards
                .Include(x => x.Tokens)
                .FirstOrDefaultAsync(
                    x => x.NormalizedWord == normalizedWord
                        && x.DeckFileName == deckFileName
                        && x.StorageMode == storageMode,
                    cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for vocabulary identity lookup", RepositoryOperations.GetByKey);
            throw new RepositoryException(nameof(VocabularyCardRepository), RepositoryOperations.GetByKey, "Failed to load vocabulary card by identity", ex);
        }
    }

    public Task AddAsync(VocabularyCard card, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(card);

        try
        {
            _logger.LogDebug("Executing {Operation} for vocabulary card {Word}", RepositoryOperations.Add, card.Word);
            _context.VocabularyCards.Add(card);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for vocabulary card {Word}", RepositoryOperations.Add, card.Word);
            throw new RepositoryException(nameof(VocabularyCardRepository), RepositoryOperations.Add, "Failed to add vocabulary card", ex);
        }
    }

    public async Task<int> CountPendingNotionSyncAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Executing {Operation} for pending Notion sync cards count", RepositoryOperations.GetActive);

            return await _context.VocabularyCards
                .Where(x => x.NotionSyncStatus == NotionSyncStatus.Pending)
                .CountAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for pending Notion sync cards count", RepositoryOperations.GetActive);
            throw new RepositoryException(nameof(VocabularyCardRepository), RepositoryOperations.GetActive, "Failed to count pending Notion sync cards", ex);
        }
    }

    public async Task<int> CountFailedNotionSyncAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Executing {Operation} for failed Notion sync cards count", RepositoryOperations.GetActive);

            return await _context.VocabularyCards
                .Where(x => x.NotionSyncStatus == NotionSyncStatus.Failed)
                .CountAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for failed Notion sync cards count", RepositoryOperations.GetActive);
            throw new RepositoryException(nameof(VocabularyCardRepository), RepositoryOperations.GetActive, "Failed to count failed Notion sync cards", ex);
        }
    }

    public async Task<IReadOnlyList<VocabularyCard>> ClaimPendingNotionSyncAsync(
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
            var candidateIds = await _context.VocabularyCards
                .AsNoTracking()
                .Where(x => x.NotionSyncStatus == NotionSyncStatus.Pending)
                .OrderBy(x => x.UpdatedAt)
                .ThenBy(x => x.Id)
                .Select(x => x.Id)
                .Take(probeTake)
                .ToListAsync(cancellationToken);

            if (candidateIds.Count == 0)
            {
                return [];
            }

            var claimed = new List<VocabularyCard>(Math.Min(take, candidateIds.Count));
            foreach (var id in candidateIds)
            {
                if (claimed.Count >= take)
                {
                    break;
                }

                var updated = await _context.VocabularyCards
                    .Where(x => x.Id == id && x.NotionSyncStatus == NotionSyncStatus.Pending)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(x => x.NotionSyncStatus, NotionSyncStatus.Processing)
                            .SetProperty(x => x.NotionAttemptCount, x => x.NotionAttemptCount + 1)
                            .SetProperty(x => x.NotionLastAttemptAtUtc, claimedAtUtc),
                        cancellationToken);

                if (updated == 0)
                {
                    continue;
                }

                var card = await _context.VocabularyCards
                    .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

                if (card is not null)
                {
                    claimed.Add(card);
                }
            }

            return claimed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for claiming pending Notion sync cards", RepositoryOperations.GetActive);
            throw new RepositoryException(nameof(VocabularyCardRepository), RepositoryOperations.GetActive, "Failed to claim pending Notion sync cards", ex);
        }
    }

    public async Task<IReadOnlyList<VocabularyCard>> GetFailedNotionSyncAsync(
        int take,
        CancellationToken cancellationToken = default)
    {
        if (take <= 0)
        {
            return [];
        }

        try
        {
            _logger.LogDebug("Executing {Operation}; Take: {Take}; Status: {Status}", RepositoryOperations.GetActive, take, NotionSyncStatus.Failed);

            return await _context.VocabularyCards
                .AsNoTracking()
                .Where(x => x.NotionSyncStatus == NotionSyncStatus.Failed)
                .OrderByDescending(x => x.NotionLastAttemptAtUtc ?? x.UpdatedAt)
                .ThenByDescending(x => x.Id)
                .Take(take)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for failed Notion sync cards", RepositoryOperations.GetActive);
            throw new RepositoryException(nameof(VocabularyCardRepository), RepositoryOperations.GetActive, "Failed to load failed Notion sync cards", ex);
        }
    }

    public async Task<int> RequeueFailedNotionSyncAsync(
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

            var candidateIds = await _context.VocabularyCards
                .AsNoTracking()
                .Where(x => x.NotionSyncStatus == NotionSyncStatus.Failed)
                .OrderByDescending(x => x.NotionLastAttemptAtUtc ?? x.UpdatedAt)
                .ThenByDescending(x => x.Id)
                .Select(x => x.Id)
                .Take(take)
                .ToListAsync(cancellationToken);

            if (candidateIds.Count == 0)
            {
                return 0;
            }

            var updated = await _context.VocabularyCards
                .Where(x => candidateIds.Contains(x.Id) && x.NotionSyncStatus == NotionSyncStatus.Failed)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(x => x.NotionSyncStatus, NotionSyncStatus.Pending)
                        .SetProperty(x => x.NotionAttemptCount, 0)
                        .SetProperty(x => x.NotionLastError, (string?)null)
                        .SetProperty(x => x.NotionLastAttemptAtUtc, (DateTimeOffset?)null),
                    cancellationToken);

            return updated;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for requeueing failed Notion sync cards", RepositoryOperations.Update);
            throw new RepositoryException(nameof(VocabularyCardRepository), RepositoryOperations.Update, "Failed to requeue failed Notion sync cards", ex);
        }
    }

    public async Task<int> CountAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Executing {Operation} for total vocabulary card count", RepositoryOperations.GetActive);
            return await _context.VocabularyCards.CountAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for total vocabulary card count", RepositoryOperations.GetActive);
            throw new RepositoryException(nameof(VocabularyCardRepository), RepositoryOperations.GetActive, "Failed to count vocabulary cards", ex);
        }
    }

    public async Task<IReadOnlyList<VocabularyCard>> GetRecentAsync(
        int take,
        CancellationToken cancellationToken = default)
    {
        if (take <= 0)
        {
            return [];
        }

        try
        {
            _logger.LogDebug("Executing {Operation}; Take: {Take}", RepositoryOperations.GetRecent, take);

            return await _context.VocabularyCards
                .AsNoTracking()
                .OrderByDescending(x => x.FirstSeenAtUtc)
                .ThenByDescending(x => x.Id)
                .Take(take)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for recent vocabulary cards", RepositoryOperations.GetRecent);
            throw new RepositoryException(nameof(VocabularyCardRepository), RepositoryOperations.GetRecent, "Failed to load recent vocabulary cards", ex);
        }
    }

    public async Task<IReadOnlyList<VocabularyDeckStat>> GetDeckStatsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Executing {Operation} for vocabulary deck stats", RepositoryOperations.GetRecent);

            return await _context.VocabularyCards
                .AsNoTracking()
                .GroupBy(card => card.DeckFileName)
                .Select(group => new VocabularyDeckStat(group.Key, group.Count()))
                .OrderByDescending(item => item.Count)
                .ThenBy(item => item.DeckFileName)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for vocabulary deck stats", RepositoryOperations.GetRecent);
            throw new RepositoryException(nameof(VocabularyCardRepository), RepositoryOperations.GetRecent, "Failed to load vocabulary deck stats", ex);
        }
    }

    public async Task<IReadOnlyList<VocabularyPartOfSpeechStat>> GetPartOfSpeechStatsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Executing {Operation} for vocabulary part-of-speech stats", RepositoryOperations.GetRecent);

            return await _context.VocabularyCards
                .AsNoTracking()
                .GroupBy(card => card.PartOfSpeechMarker)
                .Select(group => new VocabularyPartOfSpeechStat(group.Key, group.Count()))
                .OrderByDescending(item => item.Count)
                .ThenBy(item => item.Marker)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for vocabulary part-of-speech stats", RepositoryOperations.GetRecent);
            throw new RepositoryException(nameof(VocabularyCardRepository), RepositoryOperations.GetRecent, "Failed to load vocabulary part-of-speech stats", ex);
        }
    }

    public async Task<int> DeleteAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Executing {Operation} for all vocabulary cards", RepositoryOperations.Delete);
            return await _context.VocabularyCards.ExecuteDeleteAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for deleting all vocabulary cards", RepositoryOperations.Delete);
            throw new RepositoryException(nameof(VocabularyCardRepository), RepositoryOperations.Delete, "Failed to delete all vocabulary cards", ex);
        }
    }
}
