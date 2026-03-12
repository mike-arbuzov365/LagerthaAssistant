namespace LagerthaAssistant.Application.Services.Agents;

using LagerthaAssistant.Application.Interfaces.Agents;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Services.Vocabulary;

public sealed class ConversationOrchestrator : IConversationOrchestrator
{
    private readonly IReadOnlyList<IConversationAgent> _agents;

    public ConversationOrchestrator(IEnumerable<IConversationAgent> agents)
    {
        _agents = agents
            .OrderBy(agent => agent.Order)
            .ToList();
    }

    public async Task<ConversationAgentResult> ProcessAsync(string input, CancellationToken cancellationToken = default)
    {
        var normalizedInput = input?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedInput))
        {
            throw new ArgumentException("Input cannot be empty.", nameof(input));
        }

        var parsedItems = VocabularyBatchInputParser.Parse(normalizedInput);
        if (parsedItems.Count == 0)
        {
            parsedItems = [normalizedInput];
        }

        var context = new ConversationAgentContext(normalizedInput, parsedItems);

        foreach (var agent in _agents)
        {
            if (agent.CanHandle(context))
            {
                return await agent.HandleAsync(context, cancellationToken);
            }
        }

        throw new InvalidOperationException("No conversation agent is available for current input.");
    }
}
