namespace LagerthaAssistant.Application.Interfaces.Repositories;

public interface ITelegramProcessedUpdateRepository
{
    Task<bool> IsProcessedAsync(long updateId, CancellationToken cancellationToken = default);

    Task MarkProcessedAsync(long updateId, CancellationToken cancellationToken = default);

    Task DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default);
}
