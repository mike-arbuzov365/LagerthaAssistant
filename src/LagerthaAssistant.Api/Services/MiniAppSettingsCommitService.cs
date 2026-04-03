using LagerthaAssistant.Api.Contracts;
using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.Interfaces;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Vocabulary;

namespace LagerthaAssistant.Api.Services;

public sealed class MiniAppSettingsCommitService
{
    private static readonly IReadOnlyList<string> AvailableLocales =
        [LocalizationConstants.UkrainianLocale, LocalizationConstants.EnglishLocale];

    private readonly IUserLocaleStateService _localeStateService;
    private readonly IUserThemeStateService _themeStateService;
    private readonly IVocabularySaveModePreferenceService _saveModePreferenceService;
    private readonly IVocabularyStoragePreferenceService _storagePreferenceService;
    private readonly IVocabularyStorageModeProvider _storageModeProvider;
    private readonly IAiRuntimeSettingsService _aiRuntimeSettingsService;

    public MiniAppSettingsCommitService(
        IUserLocaleStateService localeStateService,
        IUserThemeStateService themeStateService,
        IVocabularySaveModePreferenceService saveModePreferenceService,
        IVocabularyStoragePreferenceService storagePreferenceService,
        IVocabularyStorageModeProvider storageModeProvider,
        IAiRuntimeSettingsService aiRuntimeSettingsService)
    {
        _localeStateService = localeStateService;
        _themeStateService = themeStateService;
        _saveModePreferenceService = saveModePreferenceService;
        _storagePreferenceService = storagePreferenceService;
        _storageModeProvider = storageModeProvider;
        _aiRuntimeSettingsService = aiRuntimeSettingsService;
    }

    public async Task<MiniAppSettingsCommitResult> CommitAsync(
        ConversationScope scope,
        MiniAppSettingsCommitRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return MiniAppSettingsCommitResult.Fail("Request body is required.");
        }

        if (!TryParseLocale(request.Locale, out var locale))
        {
            return MiniAppSettingsCommitResult.Fail(
                ApiModeValidationErrors.BuildUnsupported("locale", request.Locale, AvailableLocales));
        }

        if (!_saveModePreferenceService.TryParse(request.SaveMode, out var saveMode))
        {
            return MiniAppSettingsCommitResult.Fail(
                ApiModeValidationErrors.BuildUnsupported(
                    "save mode",
                    request.SaveMode,
                    _saveModePreferenceService.SupportedModes));
        }

        if (!_storageModeProvider.TryParse(request.StorageMode, out var storageMode))
        {
            return MiniAppSettingsCommitResult.Fail(
                ApiModeValidationErrors.BuildUnsupported(
                    "storage mode",
                    request.StorageMode,
                    _storagePreferenceService.SupportedModes));
        }

        var normalizedThemeMode = AppearanceConstants.NormalizeThemeMode(request.ThemeMode);

        if (!_aiRuntimeSettingsService.TryNormalizeProvider(request.AiProvider, out var provider))
        {
            return MiniAppSettingsCommitResult.Fail(
                ApiModeValidationErrors.BuildUnsupported(
                    "provider",
                    request.AiProvider,
                    _aiRuntimeSettingsService.SupportedProviders));
        }

        if (string.IsNullOrWhiteSpace(request.AiModel))
        {
            return MiniAppSettingsCommitResult.Fail("Model is required.");
        }

        var supportedModels = _aiRuntimeSettingsService.GetSupportedModels(provider);
        if (!supportedModels.Contains(request.AiModel, StringComparer.Ordinal))
        {
            return MiniAppSettingsCommitResult.Fail(
                ApiModeValidationErrors.BuildUnsupported(
                    "model",
                    request.AiModel,
                    supportedModels));
        }

        var persistedLocale = await _localeStateService.SetLocaleAsync(
            scope.Channel,
            scope.UserId,
            locale,
            request.SelectedManually,
            cancellationToken);

        var persistedSaveMode = await _saveModePreferenceService.SetModeAsync(scope, saveMode, cancellationToken);
        var persistedStorageMode = await _storagePreferenceService.SetModeAsync(scope, storageMode, cancellationToken);
        var persistedThemeMode = await _themeStateService.SetThemeModeAsync(scope.Channel, scope.UserId, normalizedThemeMode, cancellationToken);
        _storageModeProvider.SetMode(persistedStorageMode);

        var persistedProvider = await _aiRuntimeSettingsService.SetProviderAsync(scope, provider, cancellationToken);
        var persistedModel = await _aiRuntimeSettingsService.SetModelAsync(
            scope,
            persistedProvider,
            request.AiModel,
            cancellationToken);

        var trimmedApiKey = request.ApiKey?.Trim();
        if (!string.IsNullOrWhiteSpace(trimmedApiKey))
        {
            await _aiRuntimeSettingsService.SetApiKeyAsync(scope, persistedProvider, trimmedApiKey, cancellationToken);
        }
        else if (request.RemoveStoredKey)
        {
            await _aiRuntimeSettingsService.RemoveApiKeyAsync(scope, persistedProvider, cancellationToken);
        }

        var hasStoredKey = await _aiRuntimeSettingsService.HasStoredApiKeyAsync(scope, persistedProvider, cancellationToken);
        var apiKeySource = hasStoredKey
            ? "stored"
            : (await _aiRuntimeSettingsService.ResolveAsync(scope, cancellationToken)).ApiKeySource.ToString().ToLowerInvariant();

        return MiniAppSettingsCommitResult.Success(
            new MiniAppSettingsCommitResponse(
                LocalizationConstants.NormalizeLocaleCode(persistedLocale),
                AvailableLocales,
                _saveModePreferenceService.ToText(persistedSaveMode),
                _saveModePreferenceService.SupportedModes,
                _storageModeProvider.ToText(persistedStorageMode),
                _storagePreferenceService.SupportedModes,
                persistedThemeMode,
                AppearanceConstants.SupportedThemeModes,
                persistedProvider,
                _aiRuntimeSettingsService.SupportedProviders,
                persistedModel,
                supportedModels,
                hasStoredKey,
                apiKeySource));
    }

    private static bool TryParseLocale(string value, out string locale)
    {
        locale = LocalizationConstants.EnglishLocale;

        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.StartsWith("uk", StringComparison.Ordinal)
            || normalized.StartsWith("ua", StringComparison.Ordinal)
            || normalized.StartsWith("ru", StringComparison.Ordinal)
            || normalized.StartsWith("be", StringComparison.Ordinal))
        {
            locale = LocalizationConstants.UkrainianLocale;
            return true;
        }

        if (normalized.StartsWith("en", StringComparison.Ordinal))
        {
            locale = LocalizationConstants.EnglishLocale;
            return true;
        }

        return false;
    }
}

public sealed record MiniAppSettingsCommitResult(
    bool Succeeded,
    MiniAppSettingsCommitResponse? Response,
    string? ErrorMessage)
{
    public static MiniAppSettingsCommitResult Success(MiniAppSettingsCommitResponse response)
        => new(true, response, null);

    public static MiniAppSettingsCommitResult Fail(string errorMessage)
        => new(false, null, errorMessage);
}
