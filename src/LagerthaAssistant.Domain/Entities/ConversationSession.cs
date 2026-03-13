namespace LagerthaAssistant.Domain.Entities;

using LagerthaAssistant.Domain.Common.Base;
using LagerthaAssistant.Domain.Constants;

public sealed class ConversationSession : AuditableEntity
{
    public Guid SessionKey { get; set; }

    public string Channel { get; set; } = ConversationScopeDefaults.Channel;

    public string UserId { get; set; } = ConversationScopeDefaults.UserId;

    public string ConversationId { get; set; } = ConversationScopeDefaults.ConversationId;

    public string? Title { get; set; }

    public ICollection<ConversationHistoryEntry> Messages { get; set; } = [];

    public static ConversationSession Create(
        Guid sessionKey,
        string? title = null,
        string channel = ConversationScopeDefaults.Channel,
        string userId = ConversationScopeDefaults.UserId,
        string conversationId = ConversationScopeDefaults.ConversationId)
    {
        return new ConversationSession
        {
            SessionKey = sessionKey,
            Title = title,
            Channel = channel,
            UserId = userId,
            ConversationId = conversationId
        };
    }
}
