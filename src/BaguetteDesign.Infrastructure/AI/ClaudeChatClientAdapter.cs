namespace BaguetteDesign.Infrastructure.AI;

using SharedBotKernel.Domain.AI;
using SharedBotKernel.Infrastructure.AI;
using SharedBotKernel.Models.AI;
using SharedBotKernel.Options;

/// <summary>
/// Adapts <see cref="ClaudeChatClient"/> to <see cref="IAiChatClient"/>
/// by pre-binding the model and API key from <see cref="ClaudeOptions"/>.
/// </summary>
public sealed class ClaudeChatClientAdapter : IAiChatClient
{
    private readonly ClaudeChatClient _inner;
    private readonly ClaudeOptions _options;

    public ClaudeChatClientAdapter(ClaudeChatClient inner, ClaudeOptions options)
    {
        _inner = inner;
        _options = options;
    }

    public Task<AssistantCompletionResult> CompleteAsync(
        IReadOnlyCollection<ConversationMessage> messages,
        CancellationToken cancellationToken = default)
    {
        var apiKey = _options.ApiKey
            ?? throw new InvalidOperationException(
                "Claude API key is not configured. Set Claude:ApiKey in appsettings.");

        return _inner.CompleteAsync(messages, _options.Model, apiKey, cancellationToken);
    }
}
