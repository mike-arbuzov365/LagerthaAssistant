namespace LagerthaAssistant.Api.Contracts;

public sealed record NotionSyncStatusResponse(
    bool Enabled,
    bool IsConfigured,
    string Message,
    int PendingCards,
    int FailedCards);

