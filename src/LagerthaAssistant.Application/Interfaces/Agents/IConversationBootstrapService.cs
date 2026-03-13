namespace LagerthaAssistant.Application.Interfaces.Agents;

using LagerthaAssistant.Application.Models.Agents;

public interface IConversationBootstrapService
{
    Task<ConversationBootstrapSnapshot> BuildAsync(
        ConversationScope scope,
        ConversationBootstrapOptions? options = null,
        CancellationToken cancellationToken = default);
}
