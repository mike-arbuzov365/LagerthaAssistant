namespace LagerthaAssistant.Api.Contracts;

public sealed record PreferenceAiProviderResponse(
    string Provider,
    IReadOnlyList<string> AvailableProviders);

public sealed record PreferenceSetAiProviderRequest(
    string Provider,
    string? Channel = null,
    string? UserId = null,
    string? ConversationId = null);

public sealed record PreferenceAiModelResponse(
    string Provider,
    string Model,
    IReadOnlyList<string> AvailableModels);

public sealed record PreferenceSetAiModelRequest(
    string Model,
    string? Provider = null,
    string? Channel = null,
    string? UserId = null,
    string? ConversationId = null);

public sealed record PreferenceAiKeyStatusResponse(
    string Provider,
    bool HasStoredKey,
    string ApiKeySource);

public sealed record PreferenceSetAiKeyRequest(
    string ApiKey,
    string? Provider = null,
    string? Channel = null,
    string? UserId = null,
    string? ConversationId = null);
