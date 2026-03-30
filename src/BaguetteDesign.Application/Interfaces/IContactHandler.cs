namespace BaguetteDesign.Application.Interfaces;

public interface IContactHandler
{
    Task ShowOptionsAsync(long chatId, string? languageCode, CancellationToken cancellationToken = default);
    Task PromptForMessageAsync(long chatId, string? languageCode, CancellationToken cancellationToken = default);
    Task<bool> IsAwaitingMessageAsync(string userId, CancellationToken cancellationToken = default);
    Task HandleSendMessageAsync(long chatId, long userId, string message, string? languageCode, CancellationToken cancellationToken = default);
    Task ShowCalendarSlotsAsync(long chatId, string? languageCode, CancellationToken cancellationToken = default);
    Task BookSlotAsync(long chatId, long userId, string slotKey, string? languageCode, CancellationToken cancellationToken = default);
}
