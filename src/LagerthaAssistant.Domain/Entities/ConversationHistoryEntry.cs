namespace LagerthaAssistant.Domain.Entities;

using LagerthaAssistant.Domain.AI;
using LagerthaAssistant.Domain.Common.Base;

public sealed class ConversationHistoryEntry : AuditableEntity
{
    public int ConversationSessionId { get; set; }

    public ConversationSession ConversationSession { get; set; } = null!;

    public MessageRole Role { get; set; }

    public string Content { get; set; } = string.Empty;

    public DateTimeOffset SentAtUtc { get; set; }

    public static ConversationHistoryEntry Create(
        ConversationSession session,
        MessageRole role,
        string content,
        DateTimeOffset sentAtUtc)
    {
        return new ConversationHistoryEntry
        {
            ConversationSession = session,
            Role = role,
            Content = content,
            SentAtUtc = sentAtUtc
        };
    }
}

