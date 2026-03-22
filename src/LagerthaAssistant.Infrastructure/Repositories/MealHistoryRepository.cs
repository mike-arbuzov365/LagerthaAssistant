namespace LagerthaAssistant.Infrastructure.Repositories;

using LagerthaAssistant.Application.Interfaces.Repositories.Food;
using LagerthaAssistant.Application.Models.Food;
using LagerthaAssistant.Domain.Entities;
using LagerthaAssistant.Infrastructure.Constants;
using LagerthaAssistant.Infrastructure.Data;
using LagerthaAssistant.Infrastructure.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public sealed class MealHistoryRepository : IMealHistoryRepository
{
    private readonly AppDbContext _context;
    private readonly ILogger<MealHistoryRepository> _logger;

    public MealHistoryRepository(AppDbContext context, ILogger<MealHistoryRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public Task AddAsync(MealHistory entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        try
        {
            _logger.LogDebug("Executing {Operation} for MealHistory MealId={MealId}", RepositoryOperations.Add, entry.MealId);
            _context.MealHistory.Add(entry);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for MealHistory MealId={MealId}", RepositoryOperations.Add, entry.MealId);
            throw new RepositoryException(nameof(MealHistoryRepository), RepositoryOperations.Add, "Failed to add meal history entry", ex);
        }
    }

    public async Task<IReadOnlyList<MealHistory>> GetRangeAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Executing {Operation}; From: {From}; To: {To}", RepositoryOperations.GetRecent, from, to);
            return await _context.MealHistory
                .AsNoTracking()
                .Include(x => x.Meal)
                .Where(x => x.EatenAt >= from && x.EatenAt <= to)
                .OrderByDescending(x => x.EatenAt)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for date range {From}-{To}", RepositoryOperations.GetRecent, from, to);
            throw new RepositoryException(nameof(MealHistoryRepository), RepositoryOperations.GetRecent, "Failed to load meal history for date range", ex);
        }
    }

    public async Task<CalorieSummary> GetCalorieSummaryAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Executing {Operation} calorie summary; From: {From}; To: {To}", RepositoryOperations.GetActive, from, to);

            var entries = await _context.MealHistory
                .AsNoTracking()
                .Where(x => x.EatenAt >= from && x.EatenAt <= to && x.CaloriesConsumed.HasValue)
                .Select(x => new
                {
                    x.CaloriesConsumed,
                    x.ProteinGrams,
                    x.CarbsGrams,
                    x.FatGrams
                })
                .ToListAsync(cancellationToken);

            var totalCalories = entries.Sum(x => x.CaloriesConsumed ?? 0);
            var totalProtein = entries.Sum(x => x.ProteinGrams ?? 0);
            var totalCarbs = entries.Sum(x => x.CarbsGrams ?? 0);
            var totalFat = entries.Sum(x => x.FatGrams ?? 0);

            var days = Math.Max(1, (to.Date - from.Date).Days + 1);
            var avgPerDay = totalCalories / (decimal)days;

            return new CalorieSummary(from, to, totalCalories, avgPerDay, totalProtein, totalCarbs, totalFat);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for calorie summary {From}-{To}", RepositoryOperations.GetActive, from, to);
            throw new RepositoryException(nameof(MealHistoryRepository), RepositoryOperations.GetActive, "Failed to compute calorie summary", ex);
        }
    }

    public async Task<IReadOnlyList<MealFrequency>> GetTopMealsAsync(int take, CancellationToken cancellationToken = default)
    {
        if (take <= 0)
        {
            return [];
        }

        try
        {
            _logger.LogDebug("Executing {Operation}; Take: {Take}", RepositoryOperations.GetRecent, take);

            var rows = await _context.MealHistory
                .AsNoTracking()
                .Include(x => x.Meal)
                .GroupBy(x => new { x.MealId, x.Meal.Name })
                .Select(g => new
                {
                    g.Key.MealId,
                    g.Key.Name,
                    Count = g.Count(),
                    LastEatenAt = g.Max(x => (DateTime?)x.EatenAt)
                })
                .OrderByDescending(x => x.Count)
                .Take(take)
                .ToListAsync(cancellationToken);

            return rows
                .Select(r => new MealFrequency(r.MealId, r.Name, r.Count, r.LastEatenAt))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for top meals", RepositoryOperations.GetRecent);
            throw new RepositoryException(nameof(MealHistoryRepository), RepositoryOperations.GetRecent, "Failed to load top meals", ex);
        }
    }
}
