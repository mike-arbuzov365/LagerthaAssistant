namespace BaguetteDesign.Application.Interfaces;

public interface IInboxHandler
{
    Task ShowDialogsAsync(long chatId, string? languageCode, CancellationToken ct = default);
    Task OpenDialogAsync(long chatId, long designerUserId, string clientUserId, string? languageCode, CancellationToken ct = default);
    Task SendDraftAsync(long chatId, long designerUserId, string clientUserId, string? languageCode, CancellationToken ct = default);
    Task DismissDraftAsync(long chatId, long designerUserId, string clientUserId, string? languageCode, CancellationToken ct = default);
    Task SetManualModeAsync(long chatId, long designerUserId, string clientUserId, string? languageCode, CancellationToken ct = default);
    Task ChangeDialogStatusAsync(long chatId, string clientUserId, string newStatus, string? languageCode, CancellationToken ct = default);
    Task<bool> IsDesignerInManualModeAsync(long designerUserId, CancellationToken ct = default);
    Task HandleDesignerManualMessageAsync(long designerChatId, long designerUserId, string text, string? languageCode, CancellationToken ct = default);
}
