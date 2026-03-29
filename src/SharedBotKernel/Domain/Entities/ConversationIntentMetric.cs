namespace SharedBotKernel.Domain.Entities;

using SharedBotKernel.Domain.Base;

public sealed class ConversationIntentMetric : AuditableEntity
{
    public DateTime MetricDateUtc { get; set; }

    public string Channel { get; set; } = string.Empty;

    public string AgentName { get; set; } = string.Empty;

    public string Intent { get; set; } = string.Empty;

    public bool IsBatch { get; set; }

    public int Count { get; set; }

    public int TotalItems { get; set; }

    public DateTimeOffset LastSeenAtUtc { get; set; }
}
