namespace LagerthaAssistant.Application.Interfaces.Agents;

using LagerthaAssistant.Application.Models.Agents;

public interface IConversationMetricsService
{
    Task TrackAsync(
        string channel,
        ConversationAgentResult result,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConversationIntentMetricSummary>> GetTopIntentsAsync(
        int days,
        int take,
        string? channel = null,
        CancellationToken cancellationToken = default);
}
