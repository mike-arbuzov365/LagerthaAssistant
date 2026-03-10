namespace LagerthaAssistant.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using LagerthaAssistant.Application.Interfaces.Repositories;
using LagerthaAssistant.Domain.Entities;
using LagerthaAssistant.Infrastructure.Constants;
using LagerthaAssistant.Infrastructure.Data;
using LagerthaAssistant.Infrastructure.Exceptions;

public sealed class SystemPromptProposalRepository : ISystemPromptProposalRepository
{
    private readonly AppDbContext _context;
    private readonly ILogger<SystemPromptProposalRepository> _logger;

    public SystemPromptProposalRepository(AppDbContext context, ILogger<SystemPromptProposalRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<SystemPromptProposal?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Executing {Operation} for system prompt proposal {Id}", RepositoryOperations.GetById, id);
            return await _context.SystemPromptProposals.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for system prompt proposal {Id}", RepositoryOperations.GetById, id);
            throw new RepositoryException(nameof(SystemPromptProposalRepository), RepositoryOperations.GetById, "Failed to load system prompt proposal", ex);
        }
    }

    public async Task<IReadOnlyList<SystemPromptProposal>> GetRecentAsync(int take, CancellationToken cancellationToken = default)
    {
        if (take <= 0)
        {
            return [];
        }

        try
        {
            _logger.LogDebug("Executing {Operation} for system prompt proposals; Take: {Take}", RepositoryOperations.GetRecent, take);

            return await _context.SystemPromptProposals
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedAtUtc)
                .ThenByDescending(x => x.Id)
                .Take(take)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for system prompt proposals", RepositoryOperations.GetRecent);
            throw new RepositoryException(nameof(SystemPromptProposalRepository), RepositoryOperations.GetRecent, "Failed to load system prompt proposals", ex);
        }
    }

    public Task AddAsync(SystemPromptProposal proposal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(proposal);

        try
        {
            _logger.LogDebug("Executing {Operation} for system prompt proposal source {Source}", RepositoryOperations.Add, proposal.Source);
            _context.SystemPromptProposals.Add(proposal);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Operation} for system prompt proposal source {Source}", RepositoryOperations.Add, proposal.Source);
            throw new RepositoryException(nameof(SystemPromptProposalRepository), RepositoryOperations.Add, "Failed to add system prompt proposal", ex);
        }
    }
}
