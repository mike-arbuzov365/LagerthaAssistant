namespace LagerthaAssistant.Domain.AI;

using LagerthaAssistant.Domain.Constants;
using LagerthaAssistant.Domain.Exceptions;
using MessageRole = SharedBotKernel.Domain.AI.MessageRole;

public sealed record ConversationMessage
{
    private ConversationMessage(MessageRole role, string content, DateTimeOffset createdAtUtc)
    {
        Role = role;
        Content = content;
        CreatedAtUtc = createdAtUtc;
    }

    public MessageRole Role { get; }

    public string Content { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public static ConversationMessage Create(MessageRole role, string content, DateTimeOffset createdAtUtc)
    {
        var normalizedContent = content?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedContent))
        {
            throw new DomainValidationException("Message content cannot be empty.");
        }

        if (normalizedContent.Length > ConversationRules.MaxContentLength)
        {
            throw new DomainValidationException($"Message content exceeds max length ({ConversationRules.MaxContentLength}).");
        }

        return new ConversationMessage(role, normalizedContent, createdAtUtc);
    }
}

