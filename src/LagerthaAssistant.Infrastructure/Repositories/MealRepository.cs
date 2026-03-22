namespace LagerthaAssistant.Infrastructure.Repositories;

using LagerthaAssistant.Application.Interfaces.Repositories.Food;
using LagerthaAssistant.Domain.Entities;
using LagerthaAssistant.Infrastructure.Constants;
using LagerthaAssistant.Infrastructure.Data;
using LagerthaAssistant.Infrastructure.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public sealed class MealRepository : IMealRepository
{
    private readonly AppDbContext _context;
    private readonly ILogger<MealRepository> _logger;

    public MealRepository(AppDbContext context, ILogger<MealRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Meal?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Executing {Operation}; Id: {Id}", RepositoryOperations.GetById, id);
            return await _context.Meals
                .Include(x => x.Ingredients)
                    .ThenInclude(x => x.FoodItem)
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for Meal Id {Id}", RepositoryOperations.GetById, id);
            throw new RepositoryException(nameof(MealRepository), RepositoryOperations.GetById, "Failed to load meal by ID", ex);
        }
    }

    public async Task<Meal?> GetByNotionPageIdAsync(string notionPageId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Executing {Operation}; NotionPageId: {NotionPageId}", RepositoryOperations.GetByKey, notionPageId);
            return await _context.Meals
                .Include(x => x.Ingredients)
                .FirstOrDefaultAsync(x => x.NotionPageId == notionPageId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for NotionPageId {NotionPageId}", RepositoryOperations.GetByKey, notionPageId);
            throw new RepositoryException(nameof(MealRepository), RepositoryOperations.GetByKey, "Failed to load meal by Notion page ID", ex);
        }
    }

    public async Task<IReadOnlyList<Meal>> GetAllWithIngredientsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Executing {Operation} for all meals with ingredients", RepositoryOperations.GetRecent);
            return await _context.Meals
                .AsNoTracking()
                .Include(x => x.Ingredients)
                    .ThenInclude(x => x.FoodItem)
                .OrderBy(x => x.Name)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for all meals", RepositoryOperations.GetRecent);
            throw new RepositoryException(nameof(MealRepository), RepositoryOperations.GetRecent, "Failed to load all meals", ex);
        }
    }

    public async Task<IReadOnlyList<Meal>> GetCookableFromInventoryAsync(
        IReadOnlyCollection<int> availableFoodItemIds,
        CancellationToken cancellationToken = default)
    {
        if (availableFoodItemIds.Count == 0)
        {
            return [];
        }

        try
        {
            _logger.LogDebug(
                "Executing {Operation}; AvailableItems: {Count}",
                RepositoryOperations.GetActive,
                availableFoodItemIds.Count);

            // A meal is cookable if every ingredient's FoodItemId is in the available set.
            var availableIds = availableFoodItemIds.ToHashSet();

            var mealIds = await _context.MealIngredients
                .AsNoTracking()
                .GroupBy(x => x.MealId)
                .Where(g => g.All(mi => availableIds.Contains(mi.FoodItemId)))
                .Select(g => g.Key)
                .ToListAsync(cancellationToken);

            if (mealIds.Count == 0)
            {
                return [];
            }

            return await _context.Meals
                .AsNoTracking()
                .Include(x => x.Ingredients)
                    .ThenInclude(x => x.FoodItem)
                .Where(x => mealIds.Contains(x.Id))
                .OrderBy(x => x.Name)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for cookable meals", RepositoryOperations.GetActive);
            throw new RepositoryException(nameof(MealRepository), RepositoryOperations.GetActive, "Failed to load cookable meals", ex);
        }
    }

    public async Task<IReadOnlyList<Meal>> GetFavouritesAsync(int take, CancellationToken cancellationToken = default)
    {
        if (take <= 0)
        {
            return [];
        }

        try
        {
            _logger.LogDebug("Executing {Operation}; Take: {Take}", RepositoryOperations.GetRecent, take);

            var topMealIds = await _context.MealHistory
                .AsNoTracking()
                .GroupBy(x => x.MealId)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .Take(take)
                .ToListAsync(cancellationToken);

            if (topMealIds.Count == 0)
            {
                return await _context.Meals
                    .AsNoTracking()
                    .OrderBy(x => x.Name)
                    .Take(take)
                    .ToListAsync(cancellationToken);
            }

            return await _context.Meals
                .AsNoTracking()
                .Where(x => topMealIds.Contains(x.Id))
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for favourite meals", RepositoryOperations.GetRecent);
            throw new RepositoryException(nameof(MealRepository), RepositoryOperations.GetRecent, "Failed to load favourite meals", ex);
        }
    }

    public Task AddAsync(Meal meal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(meal);
        try
        {
            _logger.LogDebug("Executing {Operation} for meal '{Name}'", RepositoryOperations.Add, meal.Name);
            _context.Meals.Add(meal);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for meal '{Name}'", RepositoryOperations.Add, meal.Name);
            throw new RepositoryException(nameof(MealRepository), RepositoryOperations.Add, "Failed to add meal", ex);
        }
    }

    public async Task<int> DeleteAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Executing {Operation} for all meals", RepositoryOperations.Delete);
            return await _context.Meals.ExecuteDeleteAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for all meals", RepositoryOperations.Delete);
            throw new RepositoryException(nameof(MealRepository), RepositoryOperations.Delete, "Failed to delete all meals", ex);
        }
    }
}
