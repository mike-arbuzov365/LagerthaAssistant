namespace BaguetteDesign.Domain.Entities;

using BaguetteDesign.Domain.Enums;

public sealed class Notification : AuditableEntity
{
    public string UserId { get; set; } = string.Empty;
    public NotificationTrigger Trigger { get; set; }
    public string Payload { get; set; } = string.Empty;
    public DateTimeOffset ScheduledAtUtc { get; set; }
    public bool IsSent { get; set; }
    public DateTimeOffset? SentAtUtc { get; set; }
}
