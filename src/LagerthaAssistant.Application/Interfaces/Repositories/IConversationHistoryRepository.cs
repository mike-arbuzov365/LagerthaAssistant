namespace LagerthaAssistant.Application.Interfaces.Repositories;

using LagerthaAssistant.Domain.Entities;

public interface IConversationHistoryRepository
{
    Task AddAsync(ConversationHistoryEntry entry, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConversationHistoryEntry>> GetRecentBySessionIdAsync(
        int sessionId,
        int take,
        CancellationToken cancellationToken = default);
}

