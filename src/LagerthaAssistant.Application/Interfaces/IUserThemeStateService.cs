namespace LagerthaAssistant.Application.Interfaces;

public interface IUserThemeStateService
{
    Task<string> GetStoredThemeModeAsync(
        string channel,
        string userId,
        CancellationToken cancellationToken = default);

    Task<string> SetThemeModeAsync(
        string channel,
        string userId,
        string themeMode,
        CancellationToken cancellationToken = default);
}
