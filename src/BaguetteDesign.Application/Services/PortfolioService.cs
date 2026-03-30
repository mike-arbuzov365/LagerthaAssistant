namespace BaguetteDesign.Application.Services;

using BaguetteDesign.Application.Interfaces;
using BaguetteDesign.Domain.Entities;

public sealed class PortfolioService : IPortfolioService
{
    private readonly IPortfolioRepository _repo;
    private readonly INotionPortfolioClient _notionClient;

    public PortfolioService(IPortfolioRepository repo, INotionPortfolioClient notionClient)
    {
        _repo = repo;
        _notionClient = notionClient;
    }

    public async Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var categories = await _repo.GetCategoriesAsync(cancellationToken);
        if (categories.Count == 0)
        {
            await SyncAsync(cancellationToken);
            categories = await _repo.GetCategoriesAsync(cancellationToken);
        }
        return categories;
    }

    public async Task<IReadOnlyList<PortfolioCase>> GetByCategoryAsync(string category, CancellationToken cancellationToken = default)
    {
        var cases = await _repo.GetByCategoryAsync(category, cancellationToken);
        if (cases.Count == 0)
        {
            await SyncAsync(cancellationToken);
            cases = await _repo.GetByCategoryAsync(category, cancellationToken);
        }
        return cases;
    }

    private async Task SyncAsync(CancellationToken cancellationToken)
    {
        var cases = await _notionClient.FetchAllAsync(cancellationToken);
        await _repo.UpsertAsync(cases, cancellationToken);
    }
}
