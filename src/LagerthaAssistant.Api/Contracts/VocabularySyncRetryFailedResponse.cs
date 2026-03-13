namespace LagerthaAssistant.Api.Contracts;

public sealed record VocabularySyncRetryFailedResponse(
    int Requested,
    int Requeued,
    int PendingAfterRequeue);
