namespace LagerthaAssistant.Infrastructure.Repositories;

using LagerthaAssistant.Application.Interfaces.Repositories.Food;
using LagerthaAssistant.Domain.Entities;
using LagerthaAssistant.Domain.Enums;
using LagerthaAssistant.Infrastructure.Constants;
using LagerthaAssistant.Infrastructure.Data;
using LagerthaAssistant.Infrastructure.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public sealed class GroceryListRepository : IGroceryListRepository
{
    private readonly AppDbContext _context;
    private readonly ILogger<GroceryListRepository> _logger;

    public GroceryListRepository(AppDbContext context, ILogger<GroceryListRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<GroceryListItem?> GetByNotionPageIdAsync(string notionPageId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.GroceryListItems
                .FirstOrDefaultAsync(x => x.NotionPageId == notionPageId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for NotionPageId {NotionPageId}", RepositoryOperations.GetByKey, notionPageId);
            throw new RepositoryException(nameof(GroceryListRepository), RepositoryOperations.GetByKey, "Failed to load grocery item by Notion page ID", ex);
        }
    }

    public async Task<IReadOnlyList<GroceryListItem>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Executing {Operation} for active grocery list", RepositoryOperations.GetActive);
            return await _context.GroceryListItems
                .AsNoTracking()
                .Where(x => !x.IsBought)
                .OrderBy(x => x.Store)
                .ThenBy(x => x.Name)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for active grocery list", RepositoryOperations.GetActive);
            throw new RepositoryException(nameof(GroceryListRepository), RepositoryOperations.GetActive, "Failed to load active grocery list", ex);
        }
    }

    public async Task<IReadOnlyList<GroceryListItem>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.GroceryListItems
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for all grocery items", RepositoryOperations.GetRecent);
            throw new RepositoryException(nameof(GroceryListRepository), RepositoryOperations.GetRecent, "Failed to load all grocery items", ex);
        }
    }

    public async Task<int> CountPendingNotionSyncAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.GroceryListItems
                .CountAsync(x => x.NotionSyncStatus == FoodSyncStatus.Pending, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for pending grocery sync count", RepositoryOperations.GetActive);
            throw new RepositoryException(nameof(GroceryListRepository), RepositoryOperations.GetActive, "Failed to count pending grocery sync items", ex);
        }
    }

    public async Task<IReadOnlyList<GroceryListItem>> ClaimPendingNotionSyncAsync(
        int take,
        DateTime claimedAt,
        CancellationToken cancellationToken = default)
    {
        if (take <= 0)
        {
            return [];
        }

        try
        {
            var candidateIds = await _context.GroceryListItems
                .AsNoTracking()
                .Where(x => x.NotionSyncStatus == FoodSyncStatus.Pending || x.NotionSyncStatus == FoodSyncStatus.Failed)
                .OrderBy(x => x.UpdatedAt)
                .Select(x => x.Id)
                .Take(take * 3)
                .ToListAsync(cancellationToken);

            if (candidateIds.Count == 0)
            {
                return [];
            }

            var claimed = new List<GroceryListItem>(Math.Min(take, candidateIds.Count));
            foreach (var id in candidateIds)
            {
                if (claimed.Count >= take)
                {
                    break;
                }

                var updated = await _context.GroceryListItems
                    .Where(x => x.Id == id && (x.NotionSyncStatus == FoodSyncStatus.Pending || x.NotionSyncStatus == FoodSyncStatus.Failed))
                    .ExecuteUpdateAsync(
                        s => s
                            .SetProperty(x => x.NotionSyncStatus, FoodSyncStatus.Processing)
                            .SetProperty(x => x.NotionAttemptCount, x => x.NotionAttemptCount + 1)
                            .SetProperty(x => x.NotionLastAttemptAt, claimedAt),
                        cancellationToken);

                if (updated == 0)
                {
                    continue;
                }

                var item = await _context.GroceryListItems
                    .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

                if (item is not null)
                {
                    claimed.Add(item);
                }
            }

            return claimed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for claiming pending grocery sync", RepositoryOperations.GetActive);
            throw new RepositoryException(nameof(GroceryListRepository), RepositoryOperations.GetActive, "Failed to claim pending grocery sync items", ex);
        }
    }

    public Task AddAsync(GroceryListItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        try
        {
            _logger.LogDebug("Executing {Operation} for grocery item '{Name}'", RepositoryOperations.Add, item.Name);
            _context.GroceryListItems.Add(item);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for grocery item '{Name}'", RepositoryOperations.Add, item.Name);
            throw new RepositoryException(nameof(GroceryListRepository), RepositoryOperations.Add, "Failed to add grocery list item", ex);
        }
    }

    public async Task<int> MarkAllBoughtAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Executing {Operation} for marking all grocery items bought", RepositoryOperations.Update);
            return await _context.GroceryListItems
                .Where(x => !x.IsBought)
                .ExecuteUpdateAsync(
                    s => s
                        .SetProperty(x => x.IsBought, true)
                        .SetProperty(x => x.NotionSyncStatus, FoodSyncStatus.Pending),
                    cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for marking all grocery items bought", RepositoryOperations.Update);
            throw new RepositoryException(nameof(GroceryListRepository), RepositoryOperations.Update, "Failed to mark all grocery items as bought", ex);
        }
    }

    public async Task<int> MarkBoughtByIdsAsync(
        IReadOnlyCollection<int> itemIds,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (itemIds.Count == 0)
        {
            return 0;
        }

        try
        {
            _logger.LogDebug(
                "Executing {Operation} for marking selected grocery items bought; Count={Count}",
                RepositoryOperations.Update,
                itemIds.Count);

            return await _context.GroceryListItems
                .Where(x => itemIds.Contains(x.Id) && !x.IsBought)
                .ExecuteUpdateAsync(
                    s => s
                        .SetProperty(x => x.IsBought, true)
                        .SetProperty(x => x.NotionSyncStatus, FoodSyncStatus.Pending)
                        .SetProperty(x => x.NotionUpdatedAt, updatedAtUtc)
                        .SetProperty(x => x.NotionLastError, (string?)null),
                    cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for marking selected grocery items bought", RepositoryOperations.Update);
            throw new RepositoryException(nameof(GroceryListRepository), RepositoryOperations.Update, "Failed to mark selected grocery items as bought", ex);
        }
    }

    public async Task<int> DeleteBoughtAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Executing {Operation} for deleting bought grocery items", RepositoryOperations.Delete);
            return await _context.GroceryListItems
                .Where(x => x.IsBought && x.NotionSyncStatus == FoodSyncStatus.Synced)
                .ExecuteDeleteAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for deleting bought grocery items", RepositoryOperations.Delete);
            throw new RepositoryException(nameof(GroceryListRepository), RepositoryOperations.Delete, "Failed to delete bought grocery items", ex);
        }
    }

    public async Task<int> DeleteByIdsAsync(IReadOnlyCollection<int> itemIds, CancellationToken cancellationToken = default)
    {
        if (itemIds.Count == 0)
        {
            return 0;
        }

        try
        {
            _logger.LogDebug(
                "Executing {Operation} for deleting selected grocery items; Count={Count}",
                RepositoryOperations.Delete,
                itemIds.Count);

            return await _context.GroceryListItems
                .Where(x => itemIds.Contains(x.Id) && !x.IsBought)
                .ExecuteDeleteAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for deleting selected grocery items", RepositoryOperations.Delete);
            throw new RepositoryException(nameof(GroceryListRepository), RepositoryOperations.Delete, "Failed to delete selected grocery items", ex);
        }
    }

    public async Task<int> DeleteByIdsAnyStateAsync(IReadOnlyCollection<int> itemIds, CancellationToken cancellationToken = default)
    {
        if (itemIds.Count == 0)
        {
            return 0;
        }

        try
        {
            _logger.LogDebug(
                "Executing {Operation} for deleting grocery items (any state); Count={Count}",
                RepositoryOperations.Delete,
                itemIds.Count);

            return await _context.GroceryListItems
                .Where(x => itemIds.Contains(x.Id))
                .ExecuteDeleteAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for deleting grocery items (any state)", RepositoryOperations.Delete);
            throw new RepositoryException(nameof(GroceryListRepository), RepositoryOperations.Delete, "Failed to delete grocery items", ex);
        }
    }
}
