using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.Interfaces;
using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Interfaces.Repositories;
using LagerthaAssistant.Domain.Abstractions;
using LagerthaAssistant.Domain.Entities;

namespace LagerthaAssistant.Application.Services;

public sealed class UserThemeStateService : IUserThemeStateService
{
    private readonly IUserMemoryRepository _userMemoryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public UserThemeStateService(
        IUserMemoryRepository userMemoryRepository,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _userMemoryRepository = userMemoryRepository;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<string> GetStoredThemeModeAsync(
        string channel,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var entry = await _userMemoryRepository.GetByKeyAsync(
            AppearanceConstants.ThemeModeMemoryKey,
            channel,
            userId,
            cancellationToken);

        return AppearanceConstants.NormalizeThemeMode(entry?.Value);
    }

    public async Task<string> SetThemeModeAsync(
        string channel,
        string userId,
        string themeMode,
        CancellationToken cancellationToken = default)
    {
        var normalizedThemeMode = AppearanceConstants.NormalizeThemeMode(themeMode);
        var entry = await _userMemoryRepository.GetByKeyAsync(
            AppearanceConstants.ThemeModeMemoryKey,
            channel,
            userId,
            cancellationToken);

        if (entry is null)
        {
            await _userMemoryRepository.AddAsync(
                new UserMemoryEntry
                {
                    Channel = channel,
                    UserId = userId,
                    Key = AppearanceConstants.ThemeModeMemoryKey,
                    Value = normalizedThemeMode,
                    Confidence = 1.0,
                    IsActive = true,
                    LastSeenAtUtc = _clock.UtcNow
                },
                cancellationToken);
        }
        else
        {
            entry.Value = normalizedThemeMode;
            entry.Confidence = 1.0;
            entry.IsActive = true;
            entry.LastSeenAtUtc = _clock.UtcNow;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return normalizedThemeMode;
    }
}
