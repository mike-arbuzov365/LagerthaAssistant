namespace LagerthaAssistant.Application.Interfaces.Vocabulary;

using LagerthaAssistant.Application.Models.Vocabulary;

public interface INotionSyncProcessor
{
    Task<NotionSyncStatusSummary> GetStatusAsync(CancellationToken cancellationToken = default);

    Task<NotionSyncRunSummary> ProcessPendingAsync(int take, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotionSyncFailedCard>> GetFailedCardsAsync(int take, CancellationToken cancellationToken = default);

    Task<int> RequeueFailedAsync(int take, CancellationToken cancellationToken = default);
}

