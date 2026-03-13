namespace LagerthaAssistant.Application.Services.Agents;

using LagerthaAssistant.Application.Interfaces.Agents;
using LagerthaAssistant.Application.Models.Agents;

public sealed class ConversationAgentBoundaryPolicy : IConversationAgentBoundaryPolicy
{
    public bool IsAllowed(
        IConversationAgent agent,
        ConversationAgentContext context,
        ConversationCommandIntent resolvedIntent,
        out string reason)
    {
        reason = string.Empty;

        if (agent is not IConversationAgentProfile profile)
        {
            return true;
        }

        if (context.IsSlashCommand && !profile.SupportsSlashCommands)
        {
            reason = "slash-not-supported";
            return false;
        }

        if (context.IsBatch && !profile.SupportsBatchInputs)
        {
            reason = "batch-not-supported";
            return false;
        }

        var isCommandIntent = resolvedIntent.Type != ConversationCommandIntentType.Unsupported;
        if (!isCommandIntent)
        {
            return true;
        }

        if (profile.Role != ConversationAgentRole.Command)
        {
            reason = "command-intent-boundary";
            return false;
        }

        return true;
    }
}
