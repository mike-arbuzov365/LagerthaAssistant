namespace BaguetteDesign.Application.Interfaces;

public interface IProjectHandler
{
    Task ShowProjectsAsync(long chatId, string? languageCode, CancellationToken ct = default);
    Task ShowProjectCardAsync(long chatId, int projectId, string? languageCode, CancellationToken ct = default);
    Task AddRevisionAsync(long chatId, int projectId, string? languageCode, CancellationToken ct = default);
    Task ChangeProjectStatusAsync(long chatId, int projectId, string newStatus, string? languageCode, CancellationToken ct = default);
    Task ConvertLeadToProjectAsync(long chatId, int leadId, string? languageCode, CancellationToken ct = default);
}
