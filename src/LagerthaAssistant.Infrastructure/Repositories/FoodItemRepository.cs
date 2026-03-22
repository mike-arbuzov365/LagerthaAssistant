namespace LagerthaAssistant.Infrastructure.Repositories;

using LagerthaAssistant.Application.Interfaces.Repositories.Food;
using LagerthaAssistant.Domain.Entities;
using LagerthaAssistant.Infrastructure.Constants;
using LagerthaAssistant.Infrastructure.Data;
using LagerthaAssistant.Infrastructure.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public sealed class FoodItemRepository : IFoodItemRepository
{
    private readonly AppDbContext _context;
    private readonly ILogger<FoodItemRepository> _logger;

    public FoodItemRepository(AppDbContext context, ILogger<FoodItemRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<FoodItem?> GetByNotionPageIdAsync(string notionPageId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Executing {Operation}; NotionPageId: {NotionPageId}", RepositoryOperations.GetByKey, notionPageId);
            return await _context.FoodItems
                .FirstOrDefaultAsync(x => x.NotionPageId == notionPageId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for NotionPageId {NotionPageId}", RepositoryOperations.GetByKey, notionPageId);
            throw new RepositoryException(nameof(FoodItemRepository), RepositoryOperations.GetByKey, "Failed to load food item by Notion page ID", ex);
        }
    }

    public async Task<IReadOnlyList<FoodItem>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Executing {Operation} for all food items", RepositoryOperations.GetRecent);
            return await _context.FoodItems
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for all food items", RepositoryOperations.GetRecent);
            throw new RepositoryException(nameof(FoodItemRepository), RepositoryOperations.GetRecent, "Failed to load all food items", ex);
        }
    }

    public async Task<IReadOnlyList<FoodItem>> SearchByNameAsync(string query, int take = 10, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Executing {Operation}; Query: {Query}", RepositoryOperations.GetByKey, query);
            var normalized = query.Trim().ToLowerInvariant();
            return await _context.FoodItems
                .AsNoTracking()
                .Where(x => x.Name.ToLower().Contains(normalized))
                .OrderBy(x => x.Name)
                .Take(take)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for query '{Query}'", RepositoryOperations.GetByKey, query);
            throw new RepositoryException(nameof(FoodItemRepository), RepositoryOperations.GetByKey, "Failed to search food items by name", ex);
        }
    }

    public Task AddAsync(FoodItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        try
        {
            _logger.LogDebug("Executing {Operation} for food item '{Name}'", RepositoryOperations.Add, item.Name);
            _context.FoodItems.Add(item);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for food item '{Name}'", RepositoryOperations.Add, item.Name);
            throw new RepositoryException(nameof(FoodItemRepository), RepositoryOperations.Add, "Failed to add food item", ex);
        }
    }

    public async Task<int> DeleteAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Executing {Operation} for all food items", RepositoryOperations.Delete);
            return await _context.FoodItems.ExecuteDeleteAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for all food items", RepositoryOperations.Delete);
            throw new RepositoryException(nameof(FoodItemRepository), RepositoryOperations.Delete, "Failed to delete all food items", ex);
        }
    }
}
