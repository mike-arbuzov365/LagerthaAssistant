namespace BaguetteDesign.Application.Interfaces;

using BaguetteDesign.Domain.Entities;

public interface ICalendarEventRepository
{
    Task AddAsync(CalendarEvent calendarEvent, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
