using LagerthaAssistant.Application.Models.Localization;

namespace LagerthaAssistant.Application.Interfaces;

public interface IUserLocaleStateService
{
    Task<string?> GetStoredLocaleAsync(
        string channel,
        string userId,
        CancellationToken cancellationToken = default);

    Task<string> SetLocaleAsync(
        string channel,
        string userId,
        string locale,
        bool selectedManually,
        CancellationToken cancellationToken = default);

    Task<UserLocaleStateResult> EnsureLocaleAsync(
        string channel,
        string userId,
        string? telegramLanguageCode,
        string? incomingText,
        CancellationToken cancellationToken = default);
}
