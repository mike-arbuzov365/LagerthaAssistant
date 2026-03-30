namespace BaguetteDesign.Application.Interfaces;

public interface IPriceHandler
{
    Task ShowCategoriesAsync(long chatId, string? languageCode, CancellationToken cancellationToken = default);
    Task ShowCategoryItemsAsync(long chatId, string category, string? languageCode, CancellationToken cancellationToken = default);
}
