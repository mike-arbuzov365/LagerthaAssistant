using LagerthaAssistant.Application.Models.Agents;

namespace LagerthaAssistant.Api.Interfaces;

public interface ITelegramConversationResponseFormatter
{
    string Format(ConversationAgentResult result);
}
