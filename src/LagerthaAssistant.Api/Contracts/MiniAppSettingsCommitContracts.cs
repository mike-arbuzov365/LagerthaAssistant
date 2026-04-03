namespace LagerthaAssistant.Api.Contracts;

public sealed record MiniAppSettingsCommitRequest(
    string Locale,
    string SaveMode,
    string StorageMode,
    string ThemeMode,
    string AiProvider,
    string AiModel,
    string? ApiKey = null,
    bool RemoveStoredKey = false,
    bool SelectedManually = true,
    string? Channel = null,
    string? UserId = null,
    string? ConversationId = null,
    string? InitData = null);

public sealed record MiniAppSettingsCommitResponse(
    string Locale,
    IReadOnlyList<string> AvailableLocales,
    string SaveMode,
    IReadOnlyList<string> AvailableSaveModes,
    string StorageMode,
    IReadOnlyList<string> AvailableStorageModes,
    string ThemeMode,
    IReadOnlyList<string> AvailableThemeModes,
    string AiProvider,
    IReadOnlyList<string> AvailableProviders,
    string AiModel,
    IReadOnlyList<string> AvailableModels,
    bool HasStoredKey,
    string ApiKeySource);
