namespace BaguetteDesign.Infrastructure.Repositories;

using BaguetteDesign.Application.Interfaces;
using BaguetteDesign.Domain.Entities;
using BaguetteDesign.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

public sealed class NotificationRepository : INotificationRepository
{
    private readonly BaguetteDbContext _db;
    public NotificationRepository(BaguetteDbContext db) => _db = db;

    public Task AddAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        _db.Notifications.Add(notification);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<Notification>> GetDueAsync(DateTimeOffset asOf, CancellationToken cancellationToken = default)
        => await _db.Notifications
            .Where(n => !n.IsSent && n.ScheduledAtUtc <= asOf)
            .OrderBy(n => n.ScheduledAtUtc)
            .Take(50)
            .ToListAsync(cancellationToken);

    public async Task MarkSentAsync(int notificationId, CancellationToken cancellationToken = default)
    {
        var notification = await _db.Notifications.FindAsync([notificationId], cancellationToken);
        if (notification is null) return;
        notification.IsSent = true;
        notification.SentAtUtc = DateTimeOffset.UtcNow;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _db.SaveChangesAsync(cancellationToken);
}
