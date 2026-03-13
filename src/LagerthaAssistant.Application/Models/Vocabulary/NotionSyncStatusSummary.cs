namespace LagerthaAssistant.Application.Models.Vocabulary;

public sealed record NotionSyncStatusSummary(
    bool Enabled,
    bool IsConfigured,
    string Message,
    int PendingCards,
    int FailedCards);

