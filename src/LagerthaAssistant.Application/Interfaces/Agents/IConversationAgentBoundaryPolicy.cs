namespace LagerthaAssistant.Application.Interfaces.Agents;

using LagerthaAssistant.Application.Models.Agents;

public interface IConversationAgentBoundaryPolicy
{
    bool IsAllowed(
        IConversationAgent agent,
        ConversationAgentContext context,
        ConversationCommandIntent resolvedIntent,
        out string reason);
}
