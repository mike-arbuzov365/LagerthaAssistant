namespace LagerthaAssistant.Infrastructure.Repositories;

using LagerthaAssistant.Application.Interfaces.Repositories;
using LagerthaAssistant.Domain.Entities;
using LagerthaAssistant.Infrastructure.Constants;
using LagerthaAssistant.Infrastructure.Data;
using LagerthaAssistant.Infrastructure.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public sealed class TelegramProcessedUpdateRepository : ITelegramProcessedUpdateRepository
{
    private readonly AppDbContext _context;
    private readonly ILogger<TelegramProcessedUpdateRepository> _logger;

    public TelegramProcessedUpdateRepository(
        AppDbContext context,
        ILogger<TelegramProcessedUpdateRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<bool> IsProcessedAsync(long updateId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.TelegramProcessedUpdates
                .AsNoTracking()
                .AnyAsync(x => x.UpdateId == updateId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for Telegram update {UpdateId}", RepositoryOperations.GetByKey, updateId);
            throw new RepositoryException(
                nameof(TelegramProcessedUpdateRepository),
                RepositoryOperations.GetByKey,
                "Failed to check if Telegram update was already processed",
                ex);
        }
    }

    public async Task MarkProcessedAsync(long updateId, CancellationToken cancellationToken = default)
    {
        try
        {
            _context.TelegramProcessedUpdates.Add(new TelegramProcessedUpdate
            {
                UpdateId = updateId,
                ProcessedAtUtc = DateTimeOffset.UtcNow
            });
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for Telegram update {UpdateId}", RepositoryOperations.Add, updateId);
            throw new RepositoryException(
                nameof(TelegramProcessedUpdateRepository),
                RepositoryOperations.Add,
                "Failed to mark Telegram update as processed",
                ex);
        }
    }

    public async Task DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.TelegramProcessedUpdates
                .Where(x => x.ProcessedAtUtc < cutoff)
                .ExecuteDeleteAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for Telegram processed updates cleanup (cutoff={Cutoff})", RepositoryOperations.Update, cutoff);
            throw new RepositoryException(
                nameof(TelegramProcessedUpdateRepository),
                RepositoryOperations.Update,
                "Failed to delete old Telegram processed update records",
                ex);
        }
    }
}
