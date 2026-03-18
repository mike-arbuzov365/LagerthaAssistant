using LagerthaAssistant.Application.Models.Localization;

namespace LagerthaAssistant.Application.Interfaces;

public interface IUserLocaleStateService
{
    Task<UserLocaleStateResult> EnsureLocaleAsync(
        string channel,
        string userId,
        string? telegramLanguageCode,
        string? incomingText,
        CancellationToken cancellationToken = default);
}
