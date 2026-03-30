namespace SharedBotKernel.Tests.Infrastructure.AI;

using SharedBotKernel.Constants;
using SharedBotKernel.Infrastructure.AI;
using SharedBotKernel.Models.AI;
using SharedBotKernel.Models.Agents;
using Xunit;

// ResolvingAiChatClient takes concrete ClaudeChatClient / OpenAiChatClient dependencies
// (not interfaces), so routing behavior cannot be tested at the pure unit level without
// HTTP infrastructure. These tests cover the guard logic that runs before routing.
public sealed class ResolvingAiChatClientTests
{
    private static readonly IReadOnlyCollection<ConversationMessage> SampleMessages =
    [
        ConversationMessage.Create(MessageRole.System, "You are a helpful assistant.", DateTimeOffset.UtcNow),
        ConversationMessage.Create(MessageRole.User, "Hello.", DateTimeOffset.UtcNow)
    ];

    [Fact]
    public async Task CompleteAsync_WhenApiKeyIsEmpty_ThrowsBeforeRouting()
    {
        var sut = BuildSut(provider: AiProviderConstants.Claude, apiKey: "");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.CompleteAsync(SampleMessages));
    }

    [Fact]
    public async Task CompleteAsync_WhenApiKeyIsWhitespace_ThrowsBeforeRouting()
    {
        var sut = BuildSut(provider: AiProviderConstants.OpenAi, apiKey: "   ");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.CompleteAsync(SampleMessages));
    }

    [Fact]
    public async Task CompleteAsync_WhenApiKeyIsEmpty_ErrorMentionsProvider()
    {
        var sut = BuildSut(provider: AiProviderConstants.Claude, apiKey: "");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.CompleteAsync(SampleMessages));

        Assert.Contains(AiProviderConstants.Claude, ex.Message);
    }

    // ── Build ──────────────────────────────────────────────────────────────────

    private static ResolvingAiChatClient BuildSut(string provider, string apiKey)
    {
        var scope = new ConversationScope("telegram", "user-1", "conv-1");
        var settings = new AiRuntimeSettings(provider, "model-x", apiKey, AiApiKeySource.Stored);

        return new ResolvingAiChatClient(
            new FakeScopeAccessor(scope),
            new FakeRuntimeSettingsService(settings),
            openAiChatClient: null!,   // never reached — exception thrown before routing
            claudeChatClient: null!,   // never reached — exception thrown before routing
            new Microsoft.Extensions.Logging.Abstractions.NullLogger<ResolvingAiChatClient>());
    }

    // ── Fakes ──────────────────────────────────────────────────────────────────

    private sealed class FakeScopeAccessor : IConversationScopeAccessor
    {
        private ConversationScope _scope;
        public FakeScopeAccessor(ConversationScope scope) => _scope = scope;
        public ConversationScope Current => _scope;
        public void Set(ConversationScope scope) => _scope = scope;
    }

    private sealed class FakeRuntimeSettingsService : IAiRuntimeSettingsService
    {
        private readonly AiRuntimeSettings _settings;
        public FakeRuntimeSettingsService(AiRuntimeSettings settings) => _settings = settings;

        public IReadOnlyList<string> SupportedProviders => [AiProviderConstants.Claude, AiProviderConstants.OpenAi];
        public bool TryNormalizeProvider(string? value, out string provider) { provider = value ?? ""; return true; }
        public IReadOnlyList<string> GetSupportedModels(string provider) => [];
        public Task<string> GetProviderAsync(ConversationScope scope, CancellationToken ct = default) => Task.FromResult(_settings.Provider);
        public Task<string> SetProviderAsync(ConversationScope scope, string provider, CancellationToken ct = default) => Task.FromResult(provider);
        public Task<string> GetModelAsync(ConversationScope scope, string provider, CancellationToken ct = default) => Task.FromResult(_settings.Model);
        public Task<string> SetModelAsync(ConversationScope scope, string provider, string model, CancellationToken ct = default) => Task.FromResult(model);
        public Task<bool> HasStoredApiKeyAsync(ConversationScope scope, string provider, CancellationToken ct = default) => Task.FromResult(!string.IsNullOrWhiteSpace(_settings.ApiKey));
        public Task SetApiKeyAsync(ConversationScope scope, string provider, string apiKey, CancellationToken ct = default) => Task.CompletedTask;
        public Task RemoveApiKeyAsync(ConversationScope scope, string provider, CancellationToken ct = default) => Task.CompletedTask;
        public Task<AiRuntimeSettings> ResolveAsync(ConversationScope scope, CancellationToken ct = default) => Task.FromResult(_settings);
    }
}
