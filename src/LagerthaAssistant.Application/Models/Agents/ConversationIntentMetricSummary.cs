namespace LagerthaAssistant.Application.Models.Agents;

public sealed record ConversationIntentMetricSummary(
    string Channel,
    string AgentName,
    string Intent,
    bool IsBatch,
    int Count,
    int TotalItems,
    DateTimeOffset LastSeenAtUtc);
