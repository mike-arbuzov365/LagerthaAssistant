namespace LagerthaAssistant.Application.Interfaces.Repositories.Food;

using LagerthaAssistant.Domain.Entities;

public interface IGroceryListRepository
{
    Task<GroceryListItem?> GetByNotionPageIdAsync(string notionPageId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GroceryListItem>> GetActiveAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GroceryListItem>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<int> CountPendingNotionSyncAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GroceryListItem>> ClaimPendingNotionSyncAsync(int take, DateTime claimedAt, CancellationToken cancellationToken = default);

    Task AddAsync(GroceryListItem item, CancellationToken cancellationToken = default);

    Task<int> MarkBoughtByIdsAsync(IReadOnlyCollection<int> itemIds, DateTime updatedAtUtc, CancellationToken cancellationToken = default);

    Task<int> MarkAllBoughtAsync(CancellationToken cancellationToken = default);

    Task<int> DeleteBoughtAsync(CancellationToken cancellationToken = default);
}
