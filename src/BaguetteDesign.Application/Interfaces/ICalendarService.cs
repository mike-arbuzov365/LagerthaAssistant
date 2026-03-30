namespace BaguetteDesign.Application.Interfaces;

using BaguetteDesign.Domain.Models;

public interface ICalendarService
{
    Task<IReadOnlyList<CalendarSlot>> GetAvailableSlotsAsync(int daysAhead = 7, CancellationToken cancellationToken = default);
    Task<string?> BookSlotAsync(CalendarSlot slot, string clientUserId, string summary, CancellationToken cancellationToken = default);
}
