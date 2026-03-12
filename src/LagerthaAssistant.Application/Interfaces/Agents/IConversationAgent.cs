namespace LagerthaAssistant.Application.Interfaces.Agents;

using LagerthaAssistant.Application.Models.Agents;

public interface IConversationAgent
{
    string Name { get; }

    int Order { get; }

    bool CanHandle(ConversationAgentContext context);

    Task<ConversationAgentResult> HandleAsync(ConversationAgentContext context, CancellationToken cancellationToken = default);
}
