namespace LagerthaAssistant.Application.Services;

using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Models.Agents;

public sealed class ConversationScopeAccessor : IConversationScopeAccessor
{
    private ConversationScope _current = ConversationScope.Default;

    public ConversationScope Current => _current;

    public void Set(ConversationScope scope)
    {
        _current = scope ?? ConversationScope.Default;
    }
}
