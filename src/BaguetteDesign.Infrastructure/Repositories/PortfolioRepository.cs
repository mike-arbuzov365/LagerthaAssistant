namespace BaguetteDesign.Infrastructure.Repositories;

using BaguetteDesign.Application.Interfaces;
using BaguetteDesign.Domain.Entities;
using BaguetteDesign.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

public sealed class PortfolioRepository : IPortfolioRepository
{
    private readonly BaguetteDbContext _db;

    public PortfolioRepository(BaguetteDbContext db) => _db = db;

    public async Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default)
        => await _db.PortfolioCases
            .Where(x => x.IsActive)
            .Select(x => x.Category)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PortfolioCase>> GetByCategoryAsync(string category, CancellationToken cancellationToken = default)
        => await _db.PortfolioCases
            .Where(x => x.IsActive && x.Category == category)
            .OrderBy(x => x.Title)
            .ToListAsync(cancellationToken);

    public async Task UpsertAsync(IReadOnlyList<PortfolioCase> cases, CancellationToken cancellationToken = default)
    {
        var incomingIds = cases.Select(c => c.NotionPageId).ToHashSet();

        var toDeactivate = await _db.PortfolioCases
            .Where(x => !incomingIds.Contains(x.NotionPageId))
            .ToListAsync(cancellationToken);
        foreach (var item in toDeactivate)
            item.IsActive = false;

        var existing = await _db.PortfolioCases
            .Where(x => incomingIds.Contains(x.NotionPageId))
            .ToDictionaryAsync(x => x.NotionPageId, cancellationToken);

        foreach (var incoming in cases)
        {
            if (existing.TryGetValue(incoming.NotionPageId, out var stored))
            {
                stored.Title = incoming.Title;
                stored.Category = incoming.Category;
                stored.Description = incoming.Description;
                stored.Tags = incoming.Tags;
                stored.CoverImageUrl = incoming.CoverImageUrl;
                stored.IsActive = incoming.IsActive;
            }
            else
            {
                _db.PortfolioCases.Add(incoming);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
