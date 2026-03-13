namespace LagerthaAssistant.Application.Interfaces.Vocabulary;

using LagerthaAssistant.Application.Models.Vocabulary;

public interface INotionCardExportService
{
    NotionExportStatus GetStatus();

    Task<NotionCardExportResult> ExportAsync(
        NotionCardExportRequest request,
        CancellationToken cancellationToken = default);
}

