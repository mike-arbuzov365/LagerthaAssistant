namespace LagerthaAssistant.Application.Interfaces.Agents;

using LagerthaAssistant.Application.Models.Agents;

public interface IConversationIntentRouter
{
    bool TryResolve(string input, out ConversationCommandIntent intent);
}
