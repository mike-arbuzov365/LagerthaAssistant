namespace LagerthaAssistant.Application.Interfaces.Repositories;

using LagerthaAssistant.Application.Models.Agents;

public interface IConversationIntentMetricRepository
{
    Task IncrementAsync(
        DateTime metricDateUtc,
        string channel,
        string agentName,
        string intent,
        bool isBatch,
        int itemsCount,
        DateTimeOffset lastSeenAtUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConversationIntentMetricSummary>> GetTopAsync(
        DateTime fromDateUtc,
        string? channel,
        int take,
        CancellationToken cancellationToken = default);
}
