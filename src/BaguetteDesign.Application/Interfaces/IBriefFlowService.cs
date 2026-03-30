namespace BaguetteDesign.Application.Interfaces;

public interface IBriefFlowService
{
    Task<bool> IsActiveAsync(string userId, CancellationToken cancellationToken = default);
    Task StartAsync(long chatId, string userId, string? languageCode, CancellationToken cancellationToken = default);
    Task StartWithStyleAsync(long chatId, string userId, string prefilledStyle, string? languageCode, CancellationToken cancellationToken = default);
    Task HandleTextAsync(long chatId, string userId, string text, string? languageCode, CancellationToken cancellationToken = default);
    Task HandleCallbackAsync(long chatId, string userId, string callbackData, string? languageCode, CancellationToken cancellationToken = default);
}
