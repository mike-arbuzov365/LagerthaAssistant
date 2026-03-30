namespace BaguetteDesign.Application.Interfaces;

public interface ICommercialProposalHandler
{
    Task GenerateDraftAsync(long chatId, int leadId, string? languageCode, CancellationToken ct = default);
    Task SendProposalAsync(long chatId, long designerUserId, int leadId, string? languageCode, CancellationToken ct = default);
    Task DismissProposalAsync(long chatId, long designerUserId, int leadId, string? languageCode, CancellationToken ct = default);
}
