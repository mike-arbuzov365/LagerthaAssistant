namespace SharedBotKernel.Domain.AI;

using SharedBotKernel.Domain.Abstractions;
using SharedBotKernel.Domain.Constants;
using SharedBotKernel.Domain.Exceptions;

public sealed class Conversation
{
    private readonly List<ConversationMessage> _messages = [];

    public Conversation(string systemPrompt, IClock clock)
    {
        Reset(systemPrompt, clock);
    }

    public IReadOnlyCollection<ConversationMessage> Messages => _messages.AsReadOnly();

    public void AddUserMessage(string content, IClock clock)
    {
        _messages.Add(ConversationMessage.Create(MessageRole.User, content, clock.UtcNow));
    }

    public void AddAssistantMessage(string content, IClock clock)
    {
        _messages.Add(ConversationMessage.Create(MessageRole.Assistant, content, clock.UtcNow));
    }

    public void AddHistoricalMessage(MessageRole role, string content, DateTimeOffset sentAtUtc)
    {
        if (role == MessageRole.System)
        {
            return;
        }

        _messages.Add(ConversationMessage.Create(role, content, sentAtUtc));
    }

    public void Reset(string systemPrompt, IClock clock)
    {
        _messages.Clear();
        _messages.Add(ConversationMessage.Create(MessageRole.System, systemPrompt, clock.UtcNow));
    }

    public void TrimHistory(int maxMessages)
    {
        if (maxMessages < ConversationRules.MinMessagesToKeep)
        {
            throw new DomainValidationException($"Max messages must be at least {ConversationRules.MinMessagesToKeep}.");
        }

        if (_messages.Count <= maxMessages)
        {
            return;
        }

        var systemMessage = _messages[0];
        var tail = _messages.Skip(_messages.Count - (maxMessages - 1)).ToList();

        _messages.Clear();
        _messages.Add(systemMessage);
        _messages.AddRange(tail);
    }
}
