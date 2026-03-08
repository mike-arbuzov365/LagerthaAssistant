namespace LagerthaAssistant.Domain.Entities;

using LagerthaAssistant.Domain.Common.Base;

public sealed class ConversationSession : AuditableEntity
{
    public Guid SessionKey { get; set; }

    public string? Title { get; set; }

    public ICollection<ConversationHistoryEntry> Messages { get; set; } = [];

    public static ConversationSession Create(Guid sessionKey, string? title = null)
    {
        return new ConversationSession
        {
            SessionKey = sessionKey,
            Title = title
        };
    }
}

