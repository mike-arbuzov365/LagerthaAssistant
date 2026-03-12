namespace LagerthaAssistant.Application.Services.Agents;

using LagerthaAssistant.Application.Interfaces.Agents;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Services.Vocabulary;
using Microsoft.Extensions.Logging;

public sealed class ConversationOrchestrator : IConversationOrchestrator
{
    private readonly IReadOnlyList<IConversationAgent> _agents;
    private readonly ILogger<ConversationOrchestrator> _logger;

    public ConversationOrchestrator(
        IEnumerable<IConversationAgent> agents,
        ILogger<ConversationOrchestrator> logger)
    {
        _agents = agents
            .OrderBy(agent => agent.Order)
            .ToList();
        _logger = logger;
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
            if (!agent.CanHandle(context))
            {
                continue;
            }

            var result = await agent.HandleAsync(context, cancellationToken);

            _logger.LogInformation(
                "Conversation routed. Agent={Agent}; Intent={Intent}; IsBatch={IsBatch}; Items={ItemsCount}",
                result.AgentName,
                result.Intent,
                result.IsBatch,
                result.Items.Count);

            return result;
        }

        _logger.LogWarning("No conversation agent is available for input: {Input}", normalizedInput);
        throw new InvalidOperationException("No conversation agent is available for current input.");
    }
}
