namespace BaguetteDesign.Infrastructure.Repositories;

using BaguetteDesign.Application.Interfaces;
using BaguetteDesign.Domain.Entities;
using BaguetteDesign.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

public sealed class PriceRepository : IPriceRepository
{
    private readonly BaguetteDbContext _db;

    public PriceRepository(BaguetteDbContext db) => _db = db;

    public async Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default)
        => await _db.PriceItems
            .Where(x => x.IsActive)
            .Select(x => x.Category)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PriceItem>> GetByCategoryAsync(string category, CancellationToken cancellationToken = default)
        => await _db.PriceItems
            .Where(x => x.IsActive && x.Category == category)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

    public async Task UpsertAsync(IReadOnlyList<PriceItem> items, CancellationToken cancellationToken = default)
    {
        var incomingIds = items.Select(i => i.NotionPageId).ToHashSet();

        // Deactivate items removed from Notion
        var toDeactivate = await _db.PriceItems
            .Where(x => !incomingIds.Contains(x.NotionPageId))
            .ToListAsync(cancellationToken);
        foreach (var item in toDeactivate)
            item.IsActive = false;

        // Upsert each incoming item
        var existing = await _db.PriceItems
            .Where(x => incomingIds.Contains(x.NotionPageId))
            .ToDictionaryAsync(x => x.NotionPageId, cancellationToken);

        foreach (var incoming in items)
        {
            if (existing.TryGetValue(incoming.NotionPageId, out var stored))
            {
                stored.Name = incoming.Name;
                stored.Category = incoming.Category;
                stored.Description = incoming.Description;
                stored.PriceAmount = incoming.PriceAmount;
                stored.Currency = incoming.Currency;
                stored.Country = incoming.Country;
                stored.IsActive = incoming.IsActive;
            }
            else
            {
                _db.PriceItems.Add(incoming);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
