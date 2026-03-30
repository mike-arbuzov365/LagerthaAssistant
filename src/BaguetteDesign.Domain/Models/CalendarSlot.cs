namespace BaguetteDesign.Domain.Models;

public sealed record CalendarSlot(
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc)
{
    public string FormatUk() =>
        $"{StartUtc.ToLocalTime():ddd dd.MM} о {StartUtc.ToLocalTime():HH:mm}";

    public string FormatEn() =>
        $"{StartUtc.ToLocalTime():ddd MMM d} at {StartUtc.ToLocalTime():HH:mm}";
}
