namespace BaguetteDesign.Application.Interfaces;

using BaguetteDesign.Domain.Entities;

public interface INotificationRepository
{
    Task AddAsync(Notification notification, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
