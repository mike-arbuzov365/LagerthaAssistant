namespace SharedBotKernel.Infrastructure.AI;

using SharedBotKernel.Models.Agents;

public interface IConversationScopeAccessor
{
    ConversationScope Current { get; }

    void Set(ConversationScope scope);
}
