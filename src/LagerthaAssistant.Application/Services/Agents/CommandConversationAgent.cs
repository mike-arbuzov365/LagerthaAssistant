namespace LagerthaAssistant.Application.Services.Agents;

using LagerthaAssistant.Application.Interfaces.Agents;
using LagerthaAssistant.Application.Models.Agents;

public sealed class CommandConversationAgent : IConversationAgent
{
    public string Name => "command-agent";

    public int Order => 10;

    public bool CanHandle(ConversationAgentContext context)
        => context.IsSlashCommand;

    public Task<ConversationAgentResult> HandleAsync(ConversationAgentContext context, CancellationToken cancellationToken = default)
    {
        var result = ConversationAgentResult.Empty(
            Name,
            "command",
            "Slash commands are channel-specific. In API mode, send natural language requests.");

        return Task.FromResult(result);
    }
}
