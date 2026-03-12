namespace LagerthaAssistant.Application.Services.Agents;

using LagerthaAssistant.Application.Interfaces.Agents;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Services.Vocabulary;
using Microsoft.Extensions.Logging;

public sealed class ConversationOrchestrator : IConversationOrchestrator
{
    private static readonly IConversationMetricsService NoopMetricsService = new NoopConversationMetricsService();

    private readonly IReadOnlyList<IConversationAgent> _agents;
    private readonly ILogger<ConversationOrchestrator> _logger;
    private readonly IConversationMetricsService _metricsService;

    public ConversationOrchestrator(
        IEnumerable<IConversationAgent> agents,
        ILogger<ConversationOrchestrator> logger,
        IConversationMetricsService? metricsService = null)
    {
        _agents = agents
            .OrderBy(agent => agent.Order)
            .ToList();
        _logger = logger;
        _metricsService = metricsService ?? NoopMetricsService;
    }

    public Task<ConversationAgentResult> ProcessAsync(string input, CancellationToken cancellationToken = default)
        => ProcessAsync(input, "unknown", cancellationToken);

    public async Task<ConversationAgentResult> ProcessAsync(
        string input,
        string channel,
        CancellationToken cancellationToken = default)
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
                "Conversation routed. Channel={Channel}; Agent={Agent}; Intent={Intent}; IsBatch={IsBatch}; Items={ItemsCount}",
                channel,
                result.AgentName,
                result.Intent,
                result.IsBatch,
                result.Items.Count);

            await TrackMetricsSafelyAsync(channel, result, cancellationToken);
            return result;
        }

        _logger.LogWarning(
            "No conversation agent is available for input on channel {Channel}: {Input}",
            channel,
            normalizedInput);

        throw new InvalidOperationException("No conversation agent is available for current input.");
    }

    private async Task TrackMetricsSafelyAsync(
        string channel,
        ConversationAgentResult result,
        CancellationToken cancellationToken)
    {
        try
        {
            await _metricsService.TrackAsync(channel, result, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to track conversation metrics for channel {Channel}, agent {Agent}, intent {Intent}",
                channel,
                result.AgentName,
                result.Intent);
        }
    }

    private sealed class NoopConversationMetricsService : IConversationMetricsService
    {
        public Task TrackAsync(
            string channel,
            ConversationAgentResult result,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ConversationIntentMetricSummary>> GetTopIntentsAsync(
            int days,
            int take,
            string? channel = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ConversationIntentMetricSummary>>([]);
        }
    }
}
