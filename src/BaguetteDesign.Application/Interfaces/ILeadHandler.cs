namespace BaguetteDesign.Application.Interfaces;

public interface ILeadHandler
{
    Task ShowLeadsAsync(long chatId, string? languageCode, CancellationToken ct = default);
    Task ShowLeadCardAsync(long chatId, int leadId, string? languageCode, CancellationToken ct = default);
    Task ChangeLeadStatusAsync(long chatId, int leadId, string newStatus, string? languageCode, CancellationToken ct = default);
}
