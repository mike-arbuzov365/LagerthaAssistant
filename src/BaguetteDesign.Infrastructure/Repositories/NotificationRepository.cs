namespace BaguetteDesign.Infrastructure.Repositories;

using BaguetteDesign.Application.Interfaces;
using BaguetteDesign.Domain.Entities;
using BaguetteDesign.Infrastructure.Data;

public sealed class NotificationRepository : INotificationRepository
{
    private readonly BaguetteDbContext _db;
    public NotificationRepository(BaguetteDbContext db) => _db = db;

    public Task AddAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        _db.Notifications.Add(notification);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _db.SaveChangesAsync(cancellationToken);
}
