namespace LagerthaAssistant.Api.Contracts;

public sealed record VocabularySyncRunResponse(
    int Requested,
    int Processed,
    int Completed,
    int Requeued,
    int Failed,
    int PendingAfterRun);
