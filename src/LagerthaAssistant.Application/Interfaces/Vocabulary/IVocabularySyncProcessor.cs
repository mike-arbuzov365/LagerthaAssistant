namespace LagerthaAssistant.Application.Interfaces.Vocabulary;

using LagerthaAssistant.Application.Models.Vocabulary;

public interface IVocabularySyncProcessor
{
    Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default);

    Task<VocabularySyncRunSummary> ProcessPendingAsync(int take, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VocabularySyncFailedJob>> GetFailedJobsAsync(int take, CancellationToken cancellationToken = default);

    Task<int> RequeueFailedAsync(int take, CancellationToken cancellationToken = default);
}
