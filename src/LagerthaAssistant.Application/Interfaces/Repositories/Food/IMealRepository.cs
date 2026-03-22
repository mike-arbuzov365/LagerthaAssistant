namespace LagerthaAssistant.Application.Interfaces.Repositories.Food;

using LagerthaAssistant.Domain.Entities;

public interface IMealRepository
{
    Task<Meal?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<Meal?> GetByNotionPageIdAsync(string notionPageId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Meal>> GetAllWithIngredientsAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns meals whose ingredient set is fully satisfied by the given food item IDs.</summary>
    Task<IReadOnlyList<Meal>> GetCookableFromInventoryAsync(IReadOnlyCollection<int> availableFoodItemIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Meal>> GetFavouritesAsync(int take, CancellationToken cancellationToken = default);

    Task AddAsync(Meal meal, CancellationToken cancellationToken = default);

    Task<int> DeleteAllAsync(CancellationToken cancellationToken = default);
}
