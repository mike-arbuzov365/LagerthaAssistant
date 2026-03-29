namespace BaguetteDesign.Application.Interfaces;

using BaguetteDesign.Domain.Entities;

public interface INotionPriceClient
{
    Task<IReadOnlyList<PriceItem>> FetchAllAsync(CancellationToken cancellationToken = default);
}
