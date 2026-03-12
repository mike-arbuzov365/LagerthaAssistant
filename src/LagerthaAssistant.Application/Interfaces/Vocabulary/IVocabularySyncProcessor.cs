namespace LagerthaAssistant.Application.Interfaces.Vocabulary;

using LagerthaAssistant.Application.Models.Vocabulary;

public interface IVocabularySyncProcessor
{
    Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default);

    Task<VocabularySyncRunSummary> ProcessPendingAsync(int take, CancellationToken cancellationToken = default);
}
