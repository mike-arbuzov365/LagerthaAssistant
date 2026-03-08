namespace LagerthaAssistant.Application.Interfaces.AI;

using LagerthaAssistant.Application.Models.AI;
using LagerthaAssistant.Domain.AI;

public interface IAiChatClient
{
    Task<AssistantCompletionResult> CompleteAsync(
        IReadOnlyCollection<ConversationMessage> messages,
        CancellationToken cancellationToken = default);
}

