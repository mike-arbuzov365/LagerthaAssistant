namespace LagerthaAssistant.Application.Interfaces.Repositories;

using LagerthaAssistant.Domain.Entities;

public interface ISystemPromptProposalRepository
{
    Task<SystemPromptProposal?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SystemPromptProposal>> GetRecentAsync(int take, CancellationToken cancellationToken = default);

    Task AddAsync(SystemPromptProposal proposal, CancellationToken cancellationToken = default);
}
