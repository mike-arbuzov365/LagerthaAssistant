namespace LagerthaAssistant.Infrastructure.AI;

using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.Interfaces.AI;
using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Models.AI;
using LagerthaAssistant.Domain.AI;
using Microsoft.Extensions.Logging;

public sealed class ResolvingAiChatClient : IAiChatClient
{
    private readonly IConversationScopeAccessor _scopeAccessor;
    private readonly IAiRuntimeSettingsService _runtimeSettingsService;
    private readonly OpenAiChatClient _openAiChatClient;
    private readonly ClaudeChatClient _claudeChatClient;
    private readonly ILogger<ResolvingAiChatClient> _logger;

    public ResolvingAiChatClient(
        IConversationScopeAccessor scopeAccessor,
        IAiRuntimeSettingsService runtimeSettingsService,
        OpenAiChatClient openAiChatClient,
        ClaudeChatClient claudeChatClient,
        ILogger<ResolvingAiChatClient> logger)
    {
        _scopeAccessor = scopeAccessor;
        _runtimeSettingsService = runtimeSettingsService;
        _openAiChatClient = openAiChatClient;
        _claudeChatClient = claudeChatClient;
        _logger = logger;
    }

    public async Task<AssistantCompletionResult> CompleteAsync(
        IReadOnlyCollection<ConversationMessage> messages,
        CancellationToken cancellationToken = default)
    {
        var scope = _scopeAccessor.Current;
        var settings = await _runtimeSettingsService.ResolveAsync(scope, cancellationToken);
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            throw new InvalidOperationException(
                $"API key is not configured for provider '{settings.Provider}'. " +
                "Open Settings -> AI / Model / Key to set your key.");
        }

        _logger.LogDebug(
            "Resolved AI runtime settings. Channel={Channel}; UserId={UserId}; Provider={Provider}; Model={Model}; KeySource={KeySource}",
            scope.Channel,
            scope.UserId,
            settings.Provider,
            settings.Model,
            settings.ApiKeySource);

        return settings.Provider switch
        {
            AiProviderConstants.Claude => await _claudeChatClient.CompleteAsync(
                messages,
                settings.Model,
                settings.ApiKey,
                cancellationToken),
            _ => await _openAiChatClient.CompleteAsync(
                messages,
                settings.Model,
                settings.ApiKey,
                cancellationToken)
        };
    }
}
