namespace LagerthaAssistant.Application.Models.Vocabulary;

public sealed record VocabularySyncRunSummary(
    int Requested,
    int Processed,
    int Completed,
    int Requeued,
    int Failed,
    int PendingAfterRun);
