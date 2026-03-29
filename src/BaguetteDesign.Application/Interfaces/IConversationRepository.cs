namespace BaguetteDesign.Application.Interfaces;

using SharedBotKernel.Domain.Entities;

public interface IConversationRepository
{
    Task<ConversationSession> FindOrCreateSessionAsync(string userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ConversationHistoryEntry>> GetRecentHistoryAsync(int sessionId, int limit, CancellationToken cancellationToken = default);
    Task AddEntryAsync(ConversationHistoryEntry entry, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
