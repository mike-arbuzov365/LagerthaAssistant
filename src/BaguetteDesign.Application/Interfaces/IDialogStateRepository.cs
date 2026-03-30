namespace BaguetteDesign.Application.Interfaces;

using BaguetteDesign.Domain.Entities;
using BaguetteDesign.Domain.Enums;

public interface IDialogStateRepository
{
    Task<IReadOnlyList<DialogState>> GetAllAsync(CancellationToken ct = default);
    Task<DialogState?> GetByClientUserIdAsync(string clientUserId, CancellationToken ct = default);
    Task UpsertAsync(DialogState state, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
