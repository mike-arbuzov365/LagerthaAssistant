namespace LagerthaAssistant.Api.Contracts;

public sealed record SessionBootstrapRequest(
    string? Channel = null,
    string? UserId = null,
    string? ConversationId = null,
    bool IncludeCommands = false,
    bool IncludePartOfSpeechOptions = false,
    bool IncludeDecks = false,
    string? InitData = null);

public sealed record MiniAppSettingsBootstrapResponse(
    string AiProvider,
    IReadOnlyList<string> AvailableProviders,
    string AiModel,
    IReadOnlyList<string> AvailableModels,
    bool HasStoredKey,
    string ApiKeySource,
    IntegrationNotionHubStatusResponse Notion);
