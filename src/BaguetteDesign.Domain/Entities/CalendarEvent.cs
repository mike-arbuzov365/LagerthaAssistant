namespace BaguetteDesign.Domain.Entities;

public sealed class CalendarEvent : AuditableEntity
{
    public string UserId { get; set; } = string.Empty;
    public string GoogleEventId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTimeOffset StartUtc { get; set; }
    public DateTimeOffset EndUtc { get; set; }
    public string? MeetLink { get; set; }
    public string? Notes { get; set; }
    public bool ReminderSent24h { get; set; }
    public bool ReminderSent1h { get; set; }
}
