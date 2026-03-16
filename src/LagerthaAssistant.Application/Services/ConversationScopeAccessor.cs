namespace LagerthaAssistant.Application.Services;

using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Models.Agents;

public sealed class ConversationScopeAccessor : IConversationScopeAccessor
{
    private static readonly AsyncLocal<ConversationScope?> _asyncLocal = new();

    public ConversationScope Current => _asyncLocal.Value ?? ConversationScope.Default;

    public void Set(ConversationScope scope)
    {
        _asyncLocal.Value = scope ?? ConversationScope.Default;
    }
}
