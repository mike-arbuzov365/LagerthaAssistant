namespace LagerthaAssistant.Api;

using LagerthaAssistant.Application.Models.Agents;

internal static class ApiConversationScopeBuilder
{
    public static ConversationScope Build(
        string? channel,
        string? userId,
        string? conversationId,
        string defaultChannel = "api")
    {
        var normalizedChannel = channel?.Trim().ToLowerInvariant();
        var effectiveChannel = string.IsNullOrWhiteSpace(normalizedChannel)
            ? defaultChannel
            : normalizedChannel;

        return ConversationScope.Create(effectiveChannel, userId, conversationId);
    }
}
