namespace SharedBotKernel.Infrastructure.AI;

using SharedBotKernel.Models.AI;
using SharedBotKernel.Models.Agents;

public interface IAiRuntimeSettingsService
{
    IReadOnlyList<string> SupportedProviders { get; }

    bool TryNormalizeProvider(string? value, out string provider);

    IReadOnlyList<string> GetSupportedModels(string provider);

    Task<string> GetProviderAsync(ConversationScope scope, CancellationToken cancellationToken = default);

    Task<string> SetProviderAsync(
        ConversationScope scope,
        string provider,
        CancellationToken cancellationToken = default);

    Task<string> GetModelAsync(
        ConversationScope scope,
        string provider,
        CancellationToken cancellationToken = default);

    Task<string> SetModelAsync(
        ConversationScope scope,
        string provider,
        string model,
        CancellationToken cancellationToken = default);

    Task<bool> HasStoredApiKeyAsync(
        ConversationScope scope,
        string provider,
        CancellationToken cancellationToken = default);

    Task SetApiKeyAsync(
        ConversationScope scope,
        string provider,
        string apiKey,
        CancellationToken cancellationToken = default);

    Task RemoveApiKeyAsync(
        ConversationScope scope,
        string provider,
        CancellationToken cancellationToken = default);

    Task<AiRuntimeSettings> ResolveAsync(
        ConversationScope scope,
        CancellationToken cancellationToken = default);
}
