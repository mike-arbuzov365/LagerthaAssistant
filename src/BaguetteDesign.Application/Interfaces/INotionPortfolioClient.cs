namespace BaguetteDesign.Application.Interfaces;

using BaguetteDesign.Domain.Entities;

public interface INotionPortfolioClient
{
    Task<IReadOnlyList<PortfolioCase>> FetchAllAsync(CancellationToken cancellationToken = default);
}
