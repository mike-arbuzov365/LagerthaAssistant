using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.Interfaces;
using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Interfaces.Repositories;
using LagerthaAssistant.Domain.Abstractions;
using LagerthaAssistant.Domain.Entities;

namespace LagerthaAssistant.Application.Services;

public sealed class NavigationStateService : INavigationStateService
{
    private readonly IConversationSessionRepository _conversationSessionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public NavigationStateService(
        IConversationSessionRepository conversationSessionRepository,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _conversationSessionRepository = conversationSessionRepository;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<string> GetCurrentSectionAsync(
        string channel,
        string userId,
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        var session = await _conversationSessionRepository.GetLatestAsync(channel, userId, conversationId, cancellationToken);
        return session is null
            ? NavigationSections.Main
            : NavigationSections.Normalize(session.CurrentSection);
    }

    public async Task<string> SetCurrentSectionAsync(
        string channel,
        string userId,
        string conversationId,
        string section,
        CancellationToken cancellationToken = default)
    {
        var normalized = NavigationSections.Normalize(section);
        var session = await _conversationSessionRepository.GetLatestAsync(channel, userId, conversationId, cancellationToken);

        if (session is null)
        {
            session = ConversationSession.Create(
                Guid.NewGuid(),
                $"Session {_clock.UtcNow:yyyy-MM-dd HH:mm:ss}",
                channel,
                userId,
                conversationId);
            session.CurrentSection = normalized;
            await _conversationSessionRepository.AddAsync(session, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return normalized;
        }

        if (string.Equals(NavigationSections.Normalize(session.CurrentSection), normalized, StringComparison.Ordinal))
        {
            return normalized;
        }

        session.CurrentSection = normalized;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return normalized;
    }
}
