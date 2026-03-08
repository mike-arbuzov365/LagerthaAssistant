namespace LagerthaAssistant.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using LagerthaAssistant.Application.Interfaces.Repositories;
using LagerthaAssistant.Domain.Entities;
using LagerthaAssistant.Infrastructure.Constants;
using LagerthaAssistant.Infrastructure.Data;
using LagerthaAssistant.Infrastructure.Exceptions;

public sealed class ConversationSessionRepository : IConversationSessionRepository
{
    private readonly AppDbContext _context;
    private readonly ILogger<ConversationSessionRepository> _logger;

    public ConversationSessionRepository(AppDbContext context, ILogger<ConversationSessionRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ConversationSession?> GetBySessionKeyAsync(Guid sessionKey, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Executing {Operation} for SessionKey {SessionKey}", RepositoryOperations.GetBySessionKey, sessionKey);

            return await _context.ConversationSessions
                .FirstOrDefaultAsync(x => x.SessionKey == sessionKey, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for SessionKey {SessionKey}", RepositoryOperations.GetBySessionKey, sessionKey);
            throw new RepositoryException(nameof(ConversationSessionRepository), RepositoryOperations.GetBySessionKey, "Failed to load session", ex);
        }
    }

    public async Task<ConversationSession?> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Executing {Operation} for latest session", RepositoryOperations.GetLatest);

            return await _context.ConversationSessions
                .OrderByDescending(x => x.UpdatedAt)
                .FirstOrDefaultAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation}", RepositoryOperations.GetLatest);
            throw new RepositoryException(nameof(ConversationSessionRepository), RepositoryOperations.GetLatest, "Failed to load latest session", ex);
        }
    }

    public Task AddAsync(ConversationSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        try
        {
            _logger.LogDebug("Executing {Operation} for SessionKey {SessionKey}", RepositoryOperations.Add, session.SessionKey);
            _context.ConversationSessions.Add(session);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for SessionKey {SessionKey}", RepositoryOperations.Add, session.SessionKey);
            throw new RepositoryException(nameof(ConversationSessionRepository), RepositoryOperations.Add, "Failed to add session", ex);
        }
    }
}

