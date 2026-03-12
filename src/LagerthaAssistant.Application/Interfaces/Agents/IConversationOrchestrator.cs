namespace LagerthaAssistant.Application.Interfaces.Agents;

using LagerthaAssistant.Application.Models.Agents;

public interface IConversationOrchestrator
{
    Task<ConversationAgentResult> ProcessAsync(string input, CancellationToken cancellationToken = default);

    Task<ConversationAgentResult> ProcessAsync(
        string input,
        string channel,
        CancellationToken cancellationToken = default);

    Task<ConversationAgentResult> ProcessAsync(
        string input,
        string channel,
        string? userId,
        string? conversationId,
        CancellationToken cancellationToken = default);
}
