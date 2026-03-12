namespace LagerthaAssistant.Infrastructure.Repositories;

using LagerthaAssistant.Application.Interfaces.Repositories;
using LagerthaAssistant.Domain.Entities;
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
}
