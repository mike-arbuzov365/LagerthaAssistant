namespace BaguetteDesign.Application.Services;

using BaguetteDesign.Application.Interfaces;
using BaguetteDesign.Domain.Entities;
using SharedBotKernel.Infrastructure.Telegram;

public sealed class PriceService : IPriceService
{
    private readonly IPriceRepository _repo;
    private readonly INotionPriceClient _notionClient;
    private readonly ITelegramBotSender _sender;

    public PriceService(
        IPriceRepository repo,
        INotionPriceClient notionClient,
        ITelegramBotSender sender)
    {
        _repo = repo;
        _notionClient = notionClient;
        _sender = sender;
    }

    public async Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var categories = await _repo.GetCategoriesAsync(cancellationToken);
        if (categories.Count == 0)
        {
            await SyncFromNotionAsync(cancellationToken);
            categories = await _repo.GetCategoriesAsync(cancellationToken);
        }
        return categories;
    }

    public async Task<IReadOnlyList<PriceItem>> GetByCategoryAsync(string category, CancellationToken cancellationToken = default)
    {
        var items = await _repo.GetByCategoryAsync(category, cancellationToken);
        if (items.Count == 0)
        {
            await SyncFromNotionAsync(cancellationToken);
            items = await _repo.GetByCategoryAsync(category, cancellationToken);
        }
        return items;
    }

    public async Task SyncFromNotionAsync(CancellationToken cancellationToken = default)
    {
        var items = await _notionClient.FetchAllAsync(cancellationToken);
        await _repo.UpsertAsync(items, cancellationToken);
    }
}
