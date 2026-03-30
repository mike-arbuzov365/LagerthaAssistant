namespace BaguetteDesign.Application.Services;

using BaguetteDesign.Application.Interfaces;
using BaguetteDesign.Domain.Entities;
using SharedBotKernel.Infrastructure.Telegram;

public sealed class FileHandler : IFileHandler
{
    private readonly IClientFileRepository _files;
    private readonly IProjectRepository _projects;
    private readonly ITelegramBotSender _sender;

    public FileHandler(
        IClientFileRepository files,
        IProjectRepository projects,
        ITelegramBotSender sender)
    {
        _files = files;
        _projects = projects;
        _sender = sender;
    }

    public async Task HandleIncomingFileAsync(
        long chatId, long userId,
        string telegramFileId, string fileName, string? mimeType, long fileSize,
        string? languageCode, CancellationToken ct = default)
    {
        var locale = ResolveLocale(languageCode);
        var fileType = ClassifyFile(fileName, mimeType);

        // Find active project for this client
        var projects = await _projects.GetByClientUserIdAsync(userId.ToString(), ct);
        var activeProject = projects.FirstOrDefault(p =>
            p.Status is Domain.Enums.ProjectStatus.Active
                or Domain.Enums.ProjectStatus.WaitingMaterials
                or Domain.Enums.ProjectStatus.InRevision);

        var clientFile = new ClientFile
        {
            ClientUserId = userId.ToString(),
            ProjectId = activeProject?.Id,
            TelegramFileId = telegramFileId,
            FileName = fileName,
            FileType = fileType,
            MimeType = mimeType,
            FileSizeBytes = fileSize
            // GoogleDrive upload is deferred to M3 when Drive API is integrated
        };

        await _files.AddAsync(clientFile, ct);
        await _files.SaveChangesAsync(ct);

        var typeLabel = fileType switch
        {
            "text" => locale == "uk" ? "📄 Текстовий матеріал" : "📄 Text material",
            "reference" => locale == "uk" ? "🖼️ Референс" : "🖼️ Reference image",
            _ => locale == "uk" ? "📎 Файл" : "📎 File"
        };

        var projectInfo = activeProject is not null
            ? (locale == "uk" ? $" → прикріплено до проєкту #{activeProject.Id}" : $" → attached to project #{activeProject.Id}")
            : string.Empty;

        var confirm = locale == "uk"
            ? $"✅ {typeLabel} «{fileName}» отримано{projectInfo}."
            : $"✅ {typeLabel} «{fileName}» received{projectInfo}.";

        await _sender.SendTextAsync(chatId, confirm, cancellationToken: ct);
    }

    public async Task RequestMaterialsAsync(long designerChatId, string clientUserId, string? languageCode, CancellationToken ct = default)
    {
        var locale = ResolveLocale(languageCode);

        if (long.TryParse(clientUserId, out var clientChatId))
        {
            var clientMsg = locale == "uk"
                ? "📎 <b>Дизайнер запитує матеріали</b>\n\nБудь ласка, надішліть:\n• Логотипи, брендбук (якщо є)\n• Референси / приклади\n• Тексти та контент\n• Будь-які інші матеріали"
                : "📎 <b>The designer is requesting materials</b>\n\nPlease send:\n• Logos, brand book (if available)\n• References / examples\n• Texts and content\n• Any other relevant materials";
            await _sender.SendTextAsync(clientChatId, clientMsg,
                new TelegramSendOptions(ParseMode: "HTML"), cancellationToken: ct);
        }

        var designerConfirm = locale == "uk"
            ? $"✅ Запит на матеріали надіслано клієнту {clientUserId}."
            : $"✅ Materials request sent to client {clientUserId}.";
        await _sender.SendTextAsync(designerChatId, designerConfirm, cancellationToken: ct);
    }

    private static string ClassifyFile(string fileName, string? mimeType)
    {
        var ext = System.IO.Path.GetExtension(fileName).ToLowerInvariant();

        if (mimeType is not null)
        {
            if (mimeType.StartsWith("image/")) return "reference";
            if (mimeType is "application/pdf"
                or "application/msword"
                or "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
                or "text/plain") return "text";
        }

        return ext switch
        {
            ".pdf" or ".doc" or ".docx" or ".txt" or ".rtf" => "text",
            ".png" or ".jpg" or ".jpeg" or ".gif" or ".svg" or ".webp" or ".ai" or ".psd" => "reference",
            _ => "other"
        };
    }

    private static string ResolveLocale(string? languageCode)
        => languageCode?.StartsWith("uk", StringComparison.OrdinalIgnoreCase) == true ? "uk" : "en";
}
