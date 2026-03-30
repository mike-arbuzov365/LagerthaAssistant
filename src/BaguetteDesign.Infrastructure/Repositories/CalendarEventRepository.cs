namespace BaguetteDesign.Infrastructure.Repositories;

using BaguetteDesign.Application.Interfaces;
using BaguetteDesign.Domain.Entities;
using BaguetteDesign.Infrastructure.Data;

public sealed class CalendarEventRepository : ICalendarEventRepository
{
    private readonly BaguetteDbContext _db;
    public CalendarEventRepository(BaguetteDbContext db) => _db = db;

    public Task AddAsync(CalendarEvent calendarEvent, CancellationToken cancellationToken = default)
    {
        _db.CalendarEvents.Add(calendarEvent);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _db.SaveChangesAsync(cancellationToken);
}
