namespace LagerthaAssistant.Api.Contracts;

public sealed record NotionSyncRetryFailedResponse(
    int Requested,
    int Requeued,
    int PendingAfterRequeue);

