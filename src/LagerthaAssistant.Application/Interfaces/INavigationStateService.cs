namespace LagerthaAssistant.Application.Interfaces;

public interface INavigationStateService
{
    Task<string> GetCurrentSectionAsync(
        string channel,
        string userId,
        string conversationId,
        CancellationToken cancellationToken = default);

    Task<string> SetCurrentSectionAsync(
        string channel,
        string userId,
        string conversationId,
        string section,
        CancellationToken cancellationToken = default);
}
