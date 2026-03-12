namespace LagerthaAssistant.Application.Interfaces.Repositories;

using LagerthaAssistant.Domain.Entities;

public interface IConversationSessionRepository
{
    Task<ConversationSession?> GetBySessionKeyAsync(Guid sessionKey, CancellationToken cancellationToken = default);

    Task<ConversationSession?> GetLatestAsync(CancellationToken cancellationToken = default);

    Task<ConversationSession?> GetLatestAsync(
        string channel,
        string userId,
        string conversationId,
        CancellationToken cancellationToken = default);

    Task AddAsync(ConversationSession session, CancellationToken cancellationToken = default);
}
