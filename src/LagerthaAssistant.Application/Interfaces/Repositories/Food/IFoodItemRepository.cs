namespace LagerthaAssistant.Application.Interfaces.Repositories.Food;

using LagerthaAssistant.Domain.Entities;

public interface IFoodItemRepository
{
    Task<FoodItem?> GetByNotionPageIdAsync(string notionPageId, CancellationToken cancellationToken = default);

    Task<FoodItem?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FoodItem>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<int>> GetAllIdsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FoodItem>> SearchByNameAsync(string query, int take = 10, CancellationToken cancellationToken = default);

    Task<int> CountPendingNotionSyncAsync(CancellationToken cancellationToken = default);

    Task<int> CountPermanentlyFailedNotionSyncAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FoodItem>> ClaimPendingNotionSyncAsync(int take, DateTime claimedAt, CancellationToken cancellationToken = default);

    Task AddAsync(FoodItem item, CancellationToken cancellationToken = default);

    Task<int> DeleteAllAsync(CancellationToken cancellationToken = default);
}
