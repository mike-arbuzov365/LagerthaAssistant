namespace BaguetteDesign.Infrastructure.Options;

public sealed class GoogleCalendarOptions
{
    public const string SectionName = "GoogleCalendar";

    public string ServiceAccountJson { get; init; } = string.Empty;
    public string CalendarId { get; init; } = string.Empty;
    public int SlotDurationMinutes { get; init; } = 60;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ServiceAccountJson) && !string.IsNullOrWhiteSpace(CalendarId);
}
