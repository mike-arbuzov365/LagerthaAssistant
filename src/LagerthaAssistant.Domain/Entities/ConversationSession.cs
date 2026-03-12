namespace LagerthaAssistant.Domain.Entities;

using LagerthaAssistant.Domain.Common.Base;

public sealed class ConversationSession : AuditableEntity
{
    public Guid SessionKey { get; set; }

    public string Channel { get; set; } = "unknown";

    public string UserId { get; set; } = "anonymous";

    public string ConversationId { get; set; } = "default";

    public string? Title { get; set; }

    public ICollection<ConversationHistoryEntry> Messages { get; set; } = [];

    public static ConversationSession Create(
        Guid sessionKey,
        string? title = null,
        string channel = "unknown",
        string userId = "anonymous",
        string conversationId = "default")
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
