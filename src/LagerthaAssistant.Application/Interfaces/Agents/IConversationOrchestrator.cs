namespace LagerthaAssistant.Application.Interfaces.Agents;

using LagerthaAssistant.Application.Models.Agents;

public interface IConversationOrchestrator
{
    Task<ConversationAgentResult> ProcessAsync(string input, CancellationToken cancellationToken = default);
}
