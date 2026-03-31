namespace SharedBotKernel.Abstractions;

using SharedBotKernel.Domain.AI;
using SharedBotKernel.Models.AI;

public interface IAiChatClient
{
    Task<AssistantCompletionResult> CompleteAsync(
        IReadOnlyCollection<ConversationMessage> messages,
        CancellationToken cancellationToken = default);
}
