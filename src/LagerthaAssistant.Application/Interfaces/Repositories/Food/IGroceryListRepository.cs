namespace LagerthaAssistant.Application.Interfaces.Repositories.Food;

using LagerthaAssistant.Domain.Entities;

public interface IGroceryListRepository
{
    Task<GroceryListItem?> GetByNotionPageIdAsync(string notionPageId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GroceryListItem>> GetActiveAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GroceryListItem>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<int> CountPendingNotionSyncAsync(CancellationToken cancellationToken = default);

    Task<int> CountPermanentlyFailedNotionSyncAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GroceryListItem>> ClaimPendingNotionSyncAsync(int take, DateTime claimedAt, CancellationToken cancellationToken = default);

    Task AddAsync(GroceryListItem item, CancellationToken cancellationToken = default);

    Task<int> MarkBoughtByIdsAsync(IReadOnlyCollection<int> itemIds, DateTime updatedAtUtc, CancellationToken cancellationToken = default);

    Task<int> MarkAllBoughtAsync(CancellationToken cancellationToken = default);

    Task<int> DeleteBoughtAsync(CancellationToken cancellationToken = default);

    Task<int> DeleteByIdsAsync(IReadOnlyCollection<int> itemIds, CancellationToken cancellationToken = default);

    Task<int> DeleteByIdsAnyStateAsync(IReadOnlyCollection<int> itemIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true if a tombstone (soft-deleted record) exists for the given NotionPageId.
    /// Bypasses the global query filter to include archived items.
    /// </summary>
    Task<bool> ExistsArchivedByNotionPageIdAsync(string notionPageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Hard-deletes tombstones older than <paramref name="olderThan"/>.
    /// Called periodically to reclaim storage.
    /// </summary>
    Task<int> PurgeArchivedAsync(DateTime olderThan, CancellationToken cancellationToken = default);
}
