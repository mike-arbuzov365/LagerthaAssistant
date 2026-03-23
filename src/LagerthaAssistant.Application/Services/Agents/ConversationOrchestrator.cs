namespace LagerthaAssistant.Application.Services.Agents;

using LagerthaAssistant.Application.Interfaces.Agents;
using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Services.Vocabulary;
using Microsoft.Extensions.Logging;

public sealed class ConversationOrchestrator : IConversationOrchestrator
{
    private static readonly IConversationMetricsService NoopMetricsService = new NoopConversationMetricsService();
    private static readonly IConversationIntentRouter NoopIntentRouter = new NoopConversationIntentRouter();
    private static readonly IConversationAgentBoundaryPolicy PermissiveBoundaryPolicy = new PermissiveConversationAgentBoundaryPolicy();

    private readonly IReadOnlyList<IConversationAgent> _agents;
    private readonly ILogger<ConversationOrchestrator> _logger;
    private readonly IConversationMetricsService _metricsService;
    private readonly IConversationScopeAccessor _scopeAccessor;
    private readonly IConversationIntentRouter _intentRouter;
    private readonly IConversationAgentBoundaryPolicy _boundaryPolicy;

    public ConversationOrchestrator(
        IEnumerable<IConversationAgent> agents,
        ILogger<ConversationOrchestrator> logger,
        IConversationScopeAccessor scopeAccessor,
        IConversationMetricsService? metricsService = null,
        IConversationIntentRouter? intentRouter = null,
        IConversationAgentBoundaryPolicy? boundaryPolicy = null)
    {
        _agents = agents
            .OrderBy(agent => agent.Order)
            .ToList();
        _logger = logger;
        _scopeAccessor = scopeAccessor;
        _metricsService = metricsService ?? NoopMetricsService;
        _intentRouter = intentRouter ?? NoopIntentRouter;
        _boundaryPolicy = boundaryPolicy ?? PermissiveBoundaryPolicy;
    }

    public Task<ConversationAgentResult> ProcessAsync(string input, CancellationToken cancellationToken = default)
        => ProcessAsync(input, ConversationScope.DefaultChannel, null, null, cancellationToken);

    public Task<ConversationAgentResult> ProcessAsync(
        string input,
        string channel,
        CancellationToken cancellationToken = default)
        => ProcessInternalAsync(input, channel, locale: "en", userId: null, conversationId: null, cancellationToken);

    public Task<ConversationAgentResult> ProcessAsync(
        string input,
        string channel,
        string locale,
        CancellationToken cancellationToken)
        => ProcessInternalAsync(input, channel, locale, userId: null, conversationId: null, cancellationToken);

    public Task<ConversationAgentResult> ProcessAsync(
        string input,
        string channel,
        string? userId,
        string? conversationId,
        CancellationToken cancellationToken = default)
        => ProcessInternalAsync(input, channel, locale: "en", userId, conversationId, cancellationToken);

    private async Task<ConversationAgentResult> ProcessInternalAsync(
        string input,
        string channel,
        string locale,
        string? userId,
        string? conversationId,
        CancellationToken cancellationToken)
    {
        var normalizedInput = input?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedInput))
        {
            throw new ArgumentException("Input cannot be empty.", nameof(input));
        }

        var scope = ConversationScope.Create(channel, userId, conversationId);
        _scopeAccessor.Set(scope);

        var parsedItems = VocabularyBatchInputParser.Parse(normalizedInput);
        if (parsedItems.Count == 0)
        {
            parsedItems = [normalizedInput];
        }

        _intentRouter.TryResolve(normalizedInput, out var resolvedIntent);
        var context = new ConversationAgentContext(normalizedInput, parsedItems, scope, resolvedIntent, locale);

        foreach (var agent in _agents)
        {
            if (!agent.CanHandle(context))
            {
                continue;
            }

            if (!_boundaryPolicy.IsAllowed(agent, context, resolvedIntent, out var reason))
            {
                _logger.LogDebug(
                    "Conversation agent skipped by boundary policy. Channel={Channel}; Agent={Agent}; Reason={Reason}",
                    scope.Channel,
                    agent.Name,
                    reason);
                continue;
            }

            var result = await agent.HandleAsync(context, cancellationToken);

            _logger.LogInformation(
                "Conversation routed. Channel={Channel}; UserId={UserId}; ConversationId={ConversationId}; Agent={Agent}; Intent={Intent}; IsBatch={IsBatch}; Items={ItemsCount}",
                scope.Channel,
                scope.UserId,
                scope.ConversationId,
                result.AgentName,
                result.Intent,
                result.IsBatch,
                result.Items.Count);

            await TrackMetricsSafelyAsync(scope.Channel, result, cancellationToken);
            return result;
        }

        _logger.LogWarning(
            "No conversation agent is available for input on channel {Channel}, user {UserId}, conversation {ConversationId}: {Input}",
            scope.Channel,
            scope.UserId,
            scope.ConversationId,
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

    private sealed class NoopConversationIntentRouter : IConversationIntentRouter
    {
        public bool TryResolve(string input, out ConversationCommandIntent intent)
        {
            intent = new ConversationCommandIntent(ConversationCommandIntentType.Unsupported, Raw: input);
            return false;
        }
    }

    private sealed class PermissiveConversationAgentBoundaryPolicy : IConversationAgentBoundaryPolicy
    {
        public bool IsAllowed(
            IConversationAgent agent,
            ConversationAgentContext context,
            ConversationCommandIntent resolvedIntent,
            out string reason)
        {
            reason = string.Empty;
            return true;
        }
    }
}
