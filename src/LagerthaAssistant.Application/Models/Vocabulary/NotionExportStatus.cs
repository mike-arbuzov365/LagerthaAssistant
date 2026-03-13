namespace LagerthaAssistant.Application.Models.Vocabulary;

public sealed record NotionExportStatus(
    bool Enabled,
    bool IsConfigured,
    string Message);

