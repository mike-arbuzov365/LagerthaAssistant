namespace LagerthaAssistant.Application.Interfaces.Agents;

using LagerthaAssistant.Application.Models.Agents;

public interface IConversationAgentProfile
{
    ConversationAgentRole Role { get; }

    bool SupportsSlashCommands { get; }

    bool SupportsBatchInputs { get; }
}
