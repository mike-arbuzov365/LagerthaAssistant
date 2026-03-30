namespace BaguetteDesign.Application.Interfaces;

using BaguetteDesign.Domain.Entities;

public interface IPriceRepository
{
    Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PriceItem>> GetByCategoryAsync(string category, CancellationToken cancellationToken = default);
    Task UpsertAsync(IReadOnlyList<PriceItem> items, CancellationToken cancellationToken = default);
}
