namespace LagerthaAssistant.Api.Contracts;

public sealed record ConversationIntentMetricItemResponse(
    string Channel,
    string Agent,
    string Intent,
    bool IsBatch,
    int Count,
    int TotalItems,
    DateTimeOffset LastSeenAtUtc);

public sealed record ConversationIntentMetricsResponse(
    DateTime FromDateUtc,
    string? Channel,
    int Days,
    int Top,
    IReadOnlyList<ConversationIntentMetricItemResponse> Items);
