namespace LagerthaAssistant.Infrastructure.Repositories;

using LagerthaAssistant.Application.Interfaces.Repositories;
using LagerthaAssistant.Domain.Entities;
using LagerthaAssistant.Infrastructure.Data;
using LagerthaAssistant.Infrastructure.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public sealed class AiCredentialRepository : IAiCredentialRepository
{
    private readonly AppDbContext _context;
    private readonly ILogger<AiCredentialRepository> _logger;

    public AiCredentialRepository(AppDbContext context, ILogger<AiCredentialRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<UserAiCredential?> GetAsync(
        string channel,
        string userId,
        string provider,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.UserAiCredentials.FirstOrDefaultAsync(
                x => x.Channel == channel && x.UserId == userId && x.Provider == provider,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error loading AI credential. Channel={Channel}; UserId={UserId}; Provider={Provider}",
                channel,
                userId,
                provider);
            throw new RepositoryException(nameof(AiCredentialRepository), "Get", "Failed to load AI credential", ex);
        }
    }

    public Task AddAsync(UserAiCredential entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        try
        {
            _context.UserAiCredentials.Add(entry);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error adding AI credential. Channel={Channel}; UserId={UserId}; Provider={Provider}",
                entry.Channel,
                entry.UserId,
                entry.Provider);
            throw new RepositoryException(nameof(AiCredentialRepository), "Add", "Failed to add AI credential", ex);
        }
    }

    public Task RemoveAsync(UserAiCredential entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        try
        {
            _context.UserAiCredentials.Remove(entry);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error removing AI credential. Channel={Channel}; UserId={UserId}; Provider={Provider}",
                entry.Channel,
                entry.UserId,
                entry.Provider);
            throw new RepositoryException(nameof(AiCredentialRepository), "Remove", "Failed to remove AI credential", ex);
        }
    }
}
