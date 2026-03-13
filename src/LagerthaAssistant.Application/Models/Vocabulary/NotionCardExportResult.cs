namespace LagerthaAssistant.Application.Models.Vocabulary;

public enum NotionCardExportOutcome
{
    Failed = 0,
    Created = 1,
    Updated = 2,
    Skipped = 3
}

public sealed record NotionCardExportResult(
    NotionCardExportOutcome Outcome,
    bool IsRecoverableFailure,
    string? ErrorMessage = null,
    string? PageId = null)
{
    public bool Succeeded => Outcome is NotionCardExportOutcome.Created
        or NotionCardExportOutcome.Updated
        or NotionCardExportOutcome.Skipped;
}

