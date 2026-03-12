namespace LagerthaAssistant.Application.Interfaces.AI;

using LagerthaAssistant.Application.Models.AI;
using LagerthaAssistant.Domain.AI;
using LagerthaAssistant.Domain.Entities;

public interface IAssistantSessionService
{
    IReadOnlyCollection<ConversationMessage> Messages { get; }

    Task<AssistantCompletionResult> AskAsync(string userMessage, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ConversationMessage>> GetRecentHistoryAsync(
        int take,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<UserMemoryEntry>> GetActiveMemoryAsync(
        int take,
        CancellationToken cancellationToken = default);

    Task<string> GetSystemPromptAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<SystemPromptEntry>> GetSystemPromptHistoryAsync(
        int take,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<SystemPromptProposal>> GetSystemPromptProposalsAsync(
        int take,
        CancellationToken cancellationToken = default);

    Task<SystemPromptProposal> CreateSystemPromptProposalAsync(
        string prompt,
        string reason,
        double confidence,
        string source = "manual",
        CancellationToken cancellationToken = default);

    Task<SystemPromptProposal> GenerateSystemPromptProposalAsync(
        string goal,
        CancellationToken cancellationToken = default);

    Task<string> ApplySystemPromptProposalAsync(
        int proposalId,
        CancellationToken cancellationToken = default);

    Task RejectSystemPromptProposalAsync(
        int proposalId,
        CancellationToken cancellationToken = default);

    Task<string> SetSystemPromptAsync(
        string prompt,
        string source = "manual",
        CancellationToken cancellationToken = default);

    void Reset();
}
