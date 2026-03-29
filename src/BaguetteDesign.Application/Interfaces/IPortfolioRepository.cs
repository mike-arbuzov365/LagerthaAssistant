namespace BaguetteDesign.Application.Interfaces;

using BaguetteDesign.Domain.Entities;

public interface IPortfolioRepository
{
    Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PortfolioCase>> GetByCategoryAsync(string category, CancellationToken cancellationToken = default);
    Task UpsertAsync(IReadOnlyList<PortfolioCase> cases, CancellationToken cancellationToken = default);
}
