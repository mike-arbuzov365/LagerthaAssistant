namespace LagerthaAssistant.Application.Interfaces.Common;

using LagerthaAssistant.Application.Models.Agents;

public interface IConversationScopeAccessor
{
    ConversationScope Current { get; }

    void Set(ConversationScope scope);
}
