namespace BaguetteDesign.Application.Interfaces;

using BaguetteDesign.Domain.Entities;

public interface IPortfolioService
{
    Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PortfolioCase>> GetByCategoryAsync(string category, CancellationToken cancellationToken = default);
}
