namespace LagerthaAssistant.Application.Models.Agents;

using LagerthaAssistant.Domain.Constants;

public sealed record ConversationScope(
    string Channel,
    string UserId,
    string ConversationId)
{
    public const string DefaultChannel = ConversationScopeDefaults.Channel;
    public const string DefaultUserId = ConversationScopeDefaults.UserId;
    public const string DefaultConversationId = ConversationScopeDefaults.ConversationId;

    public static ConversationScope Default { get; } = new(DefaultChannel, DefaultUserId, DefaultConversationId);

    public static ConversationScope Create(
        string? channel,
        string? userId,
        string? conversationId)
    {
        return new ConversationScope(
            NormalizeOrDefault(channel, DefaultChannel),
            NormalizeOrDefault(userId, DefaultUserId),
            NormalizeOrDefault(conversationId, DefaultConversationId));
    }

    private static string NormalizeOrDefault(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return value.Trim().ToLowerInvariant();
    }
}
