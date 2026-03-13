namespace LagerthaAssistant.Api.Contracts;

public sealed record NotionSyncRunResponse(
    int Requested,
    int Processed,
    int Completed,
    int Requeued,
    int Failed,
    int PendingAfterRun);

