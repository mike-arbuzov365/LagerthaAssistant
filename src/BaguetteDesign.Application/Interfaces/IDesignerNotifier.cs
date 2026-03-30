namespace BaguetteDesign.Application.Interfaces;

using BaguetteDesign.Domain.Models;

public interface IDesignerNotifier
{
    Task NotifyMessageReceivedAsync(long clientUserId, string message, CancellationToken cancellationToken = default);
    Task NotifySlotBookedAsync(long clientUserId, CalendarSlot slot, string? meetLink, CancellationToken cancellationToken = default);
}
