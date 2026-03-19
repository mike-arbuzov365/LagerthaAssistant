using System.Text.RegularExpressions;
using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.Interfaces;
using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Interfaces.Repositories;
using LagerthaAssistant.Application.Models.Localization;
using LagerthaAssistant.Domain.Abstractions;
using LagerthaAssistant.Domain.Entities;

namespace LagerthaAssistant.Application.Services;

public sealed class UserLocaleStateService : IUserLocaleStateService
{
    private static readonly Regex UkrainianSpecificRegex = new("[іїєґ]", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex LatinRegex = new("[a-z]", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex CyrillicRegex = new("[\\u0400-\\u04FF]", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IUserMemoryRepository _userMemoryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILocalizationService _localizationService;
    private readonly IClock _clock;

    public UserLocaleStateService(
        IUserMemoryRepository userMemoryRepository,
        IUnitOfWork unitOfWork,
        ILocalizationService localizationService,
        IClock clock)
    {
        _userMemoryRepository = userMemoryRepository;
        _unitOfWork = unitOfWork;
        _localizationService = localizationService;
        _clock = clock;
    }

    public async Task<UserLocaleStateResult> EnsureLocaleAsync(
        string channel,
        string userId,
        string? telegramLanguageCode,
        string? incomingText,
        CancellationToken cancellationToken = default)
    {
        var localeEntry = await _userMemoryRepository.GetByKeyAsync(LocalizationConstants.LocaleMemoryKey, channel, userId, cancellationToken);
        if (localeEntry is null)
        {
            var initialLocale = LocalizationConstants.NormalizeLocaleCode(_localizationService.GetLocaleForUser(telegramLanguageCode));
            await UpsertLocaleEntryAsync(channel, userId, initialLocale, cancellationToken);
            return new UserLocaleStateResult(initialLocale, IsInitialized: true, IsSwitched: false);
        }

        var persistedLocale = LocalizationConstants.NormalizeLocaleCode(localeEntry.Value);
        var manualSelectionEntry = await _userMemoryRepository.GetByKeyAsync(
            LocalizationConstants.LocaleSelectedManuallyMemoryKey,
            channel,
            userId,
            cancellationToken);

        if (IsManualSelectionEnabled(manualSelectionEntry?.Value))
        {
            localeEntry.LastSeenAtUtc = _clock.UtcNow;
            localeEntry.Confidence = 1.0;

            if (!string.Equals(localeEntry.Value, persistedLocale, StringComparison.Ordinal))
            {
                localeEntry.Value = persistedLocale;
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new UserLocaleStateResult(persistedLocale, IsInitialized: false, IsSwitched: false);
        }

        var currentLocale = NormalizeLocale(persistedLocale);

        var detectedLocale = DetectLocaleFromText(incomingText);
        var changed = false;
        var switched = false;

        if (detectedLocale is null)
        {
            localeEntry.LastSeenAtUtc = _clock.UtcNow;
            changed = true;
        }
        else if (string.Equals(detectedLocale, currentLocale, StringComparison.Ordinal))
        {
            if (Math.Abs(localeEntry.Confidence - 1.0) > double.Epsilon)
            {
                localeEntry.Confidence = 1.0;
                changed = true;
            }

            localeEntry.LastSeenAtUtc = _clock.UtcNow;
            changed = true;
        }
        else
        {
            var nextConfidence = localeEntry.Confidence >= 1.0
                ? 0.75
                : Math.Min(1.0, localeEntry.Confidence + 0.25);

            localeEntry.Confidence = nextConfidence;
            localeEntry.LastSeenAtUtc = _clock.UtcNow;
            changed = true;

            if (nextConfidence >= 1.0)
            {
                localeEntry.Value = detectedLocale;
                currentLocale = detectedLocale;
                switched = true;
            }
        }

        if (changed)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new UserLocaleStateResult(currentLocale, IsInitialized: false, switched);
    }

    public async Task<string?> GetStoredLocaleAsync(
        string channel,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var localeEntry = await _userMemoryRepository.GetByKeyAsync(
            LocalizationConstants.LocaleMemoryKey,
            channel,
            userId,
            cancellationToken);

        return localeEntry is null
            ? null
            : LocalizationConstants.NormalizeLocaleCode(localeEntry.Value);
    }

    public async Task<string> SetLocaleAsync(
        string channel,
        string userId,
        string locale,
        bool selectedManually,
        CancellationToken cancellationToken = default)
    {
        var normalizedLocale = LocalizationConstants.NormalizeLocaleCode(locale);
        var localeEntry = await UpsertLocaleEntryAsync(channel, userId, normalizedLocale, cancellationToken);

        if (!string.Equals(localeEntry.Value, normalizedLocale, StringComparison.Ordinal))
        {
            localeEntry.Value = normalizedLocale;
        }

        localeEntry.Confidence = 1.0;
        localeEntry.IsActive = true;
        localeEntry.LastSeenAtUtc = _clock.UtcNow;

        var manualEntry = await _userMemoryRepository.GetByKeyAsync(
            LocalizationConstants.LocaleSelectedManuallyMemoryKey,
            channel,
            userId,
            cancellationToken);

        if (manualEntry is null)
        {
            await _userMemoryRepository.AddAsync(
                new UserMemoryEntry
                {
                    Channel = channel,
                    UserId = userId,
                    Key = LocalizationConstants.LocaleSelectedManuallyMemoryKey,
                    Value = selectedManually ? "true" : "false",
                    Confidence = 1.0,
                    IsActive = true,
                    LastSeenAtUtc = _clock.UtcNow
                },
                cancellationToken);
        }
        else
        {
            manualEntry.Value = selectedManually ? "true" : "false";
            manualEntry.Confidence = 1.0;
            manualEntry.IsActive = true;
            manualEntry.LastSeenAtUtc = _clock.UtcNow;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return normalizedLocale;
    }

    private async Task<UserMemoryEntry> UpsertLocaleEntryAsync(
        string channel,
        string userId,
        string locale,
        CancellationToken cancellationToken)
    {
        var localeEntry = await _userMemoryRepository.GetByKeyAsync(
            LocalizationConstants.LocaleMemoryKey,
            channel,
            userId,
            cancellationToken);

        if (localeEntry is not null)
        {
            localeEntry.Value = locale;
            localeEntry.Confidence = 1.0;
            localeEntry.IsActive = true;
            localeEntry.LastSeenAtUtc = _clock.UtcNow;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return localeEntry;
        }

        localeEntry = new UserMemoryEntry
        {
            Channel = channel,
            UserId = userId,
            Key = LocalizationConstants.LocaleMemoryKey,
            Value = locale,
            Confidence = 1.0,
            IsActive = true,
            LastSeenAtUtc = _clock.UtcNow
        };

        await _userMemoryRepository.AddAsync(localeEntry, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return localeEntry;
    }

    private static bool IsManualSelectionEnabled(string? value)
        => string.Equals(value?.Trim(), "true", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeLocale(string? value)
    {
        var normalized = LocalizationConstants.NormalizeLocaleCode(value);

        // Automatic switching currently supports only "en" and "uk".
        // Other locales remain user-selected and should not be auto-detected.
        return normalized is LocalizationConstants.UkrainianLocale
            ? LocalizationConstants.UkrainianLocale
            : LocalizationConstants.EnglishLocale;
    }

    private static string? DetectLocaleFromText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (UkrainianSpecificRegex.IsMatch(text))
        {
            return LocalizationConstants.UkrainianLocale;
        }

        if (LatinRegex.IsMatch(text))
        {
            return LocalizationConstants.EnglishLocale;
        }

        // Generic Cyrillic without Ukrainian-specific letters should not trigger
        // switching to any language. Russian is intentionally never supported.
        if (CyrillicRegex.IsMatch(text))
        {
            return null;
        }

        return null;
    }
}
