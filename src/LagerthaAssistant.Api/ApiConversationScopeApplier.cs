namespace LagerthaAssistant.Api;

using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Models.Agents;

internal static class ApiConversationScopeApplier
{
    public static ConversationScope Apply(
        IConversationScopeAccessor scopeAccessor,
        string? channel,
        string? userId,
        string? conversationId)
    {
        var scope = ApiConversationScopeBuilder.Build(channel, userId, conversationId);
        scopeAccessor.Set(scope);
        return scope;
    }
}
