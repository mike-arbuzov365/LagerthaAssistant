namespace LagerthaAssistant.Application.Interfaces.Repositories.Food;

using LagerthaAssistant.Application.Models.Food;
using LagerthaAssistant.Domain.Entities;

public interface IMealHistoryRepository
{
    Task AddAsync(MealHistory entry, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MealHistory>> GetRangeAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);

    Task<CalorieSummary> GetCalorieSummaryAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MealFrequency>> GetTopMealsAsync(int take, CancellationToken cancellationToken = default);
}
