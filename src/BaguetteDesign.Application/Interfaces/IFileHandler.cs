namespace BaguetteDesign.Application.Interfaces;

public interface IFileHandler
{
    Task HandleIncomingFileAsync(long chatId, long userId, string telegramFileId, string fileName, string? mimeType, long fileSize, string? languageCode, CancellationToken ct = default);
    Task RequestMaterialsAsync(long designerChatId, string clientUserId, string? languageCode, CancellationToken ct = default);
}
