namespace LagerthaAssistant.IntegrationTests.Controllers;

using LagerthaAssistant.Api.Contracts;
using LagerthaAssistant.Api.Controllers;
using LagerthaAssistant.Application.Interfaces.Common;
using Microsoft.AspNetCore.Mvc;
using Xunit;

public sealed class PreferenceAiControllerTests
{
    [Fact]
    public async Task GetProvider_ShouldReturnCurrentProvider()
    {
        var scopeAccessor = new FakeConversationScopeAccessor();
        var aiService = new FakeAiRuntimeSettingsService
        {
            CurrentProvider = "claude"
        };
        var sut = new PreferenceAiController(scopeAccessor, aiService);

        var response = await sut.GetProvider(cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<PreferenceAiProviderResponse>(ok.Value);

        Assert.Equal("claude", payload.Provider);
        Assert.Equal(["openai", "claude"], payload.AvailableProviders);
    }

    [Fact]
    public async Task SetProvider_ShouldReturnBadRequest_WhenUnsupported()
    {
        var scopeAccessor = new FakeConversationScopeAccessor();
        var aiService = new FakeAiRuntimeSettingsService();
        var sut = new PreferenceAiController(scopeAccessor, aiService);

        var response = await sut.SetProvider(new PreferenceSetAiProviderRequest("gemini"), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(response.Result);
        Assert.Equal("Unsupported provider 'gemini'. Use one of: openai, claude.", badRequest.Value);
        Assert.Null(aiService.LastSetProviderCall);
    }

    [Fact]
    public async Task SetProvider_ShouldPersistProvider_WhenValid()
    {
        var scopeAccessor = new FakeConversationScopeAccessor();
        var aiService = new FakeAiRuntimeSettingsService();
        var sut = new PreferenceAiController(scopeAccessor, aiService);

        var response = await sut.SetProvider(
            new PreferenceSetAiProviderRequest("Anthropic", " telegram ", "Mike", "chat-42"),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<PreferenceAiProviderResponse>(ok.Value);

        Assert.Equal("claude", payload.Provider);
        Assert.NotNull(aiService.LastSetProviderCall);
        Assert.Equal("telegram", aiService.LastSetProviderCall!.Scope.Channel);
        Assert.Equal("mike", aiService.LastSetProviderCall!.Scope.UserId);
        Assert.Equal("claude", aiService.LastSetProviderCall!.Provider);
    }

    [Fact]
    public async Task GetModel_ShouldUseCurrentProvider_WhenProviderNotPassed()
    {
        var scopeAccessor = new FakeConversationScopeAccessor();
        var aiService = new FakeAiRuntimeSettingsService
        {
            CurrentProvider = "openai",
            CurrentModel = "gpt-4.1-mini"
        };
        var sut = new PreferenceAiController(scopeAccessor, aiService);

        var response = await sut.GetModel(cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<PreferenceAiModelResponse>(ok.Value);

        Assert.Equal("openai", payload.Provider);
        Assert.Equal("gpt-4.1-mini", payload.Model);
        Assert.Contains("gpt-4.1-mini", payload.AvailableModels);
    }

    [Fact]
    public async Task SetModel_ShouldReturnBadRequest_WhenModelMissing()
    {
        var scopeAccessor = new FakeConversationScopeAccessor();
        var aiService = new FakeAiRuntimeSettingsService();
        var sut = new PreferenceAiController(scopeAccessor, aiService);

        var response = await sut.SetModel(new PreferenceSetAiModelRequest(" "), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(response.Result);
        Assert.Null(aiService.LastSetModelCall);
    }

    [Fact]
    public async Task GetKeyStatus_ShouldReturnStoredFlagAndSource()
    {
        var scopeAccessor = new FakeConversationScopeAccessor();
        var aiService = new FakeAiRuntimeSettingsService
        {
            CurrentProvider = "claude",
            HasStoredKey = true,
            RuntimeApiKeySource = AiApiKeySource.Stored
        };
        var sut = new PreferenceAiController(scopeAccessor, aiService);

        var response = await sut.GetKeyStatus(cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<PreferenceAiKeyStatusResponse>(ok.Value);

        Assert.Equal("claude", payload.Provider);
        Assert.True(payload.HasStoredKey);
        Assert.Equal("stored", payload.ApiKeySource);
    }

    [Fact]
    public async Task SetApiKey_ShouldPersistForResolvedProvider()
    {
        var scopeAccessor = new FakeConversationScopeAccessor();
        var aiService = new FakeAiRuntimeSettingsService
        {
            CurrentProvider = "openai"
        };
        var sut = new PreferenceAiController(scopeAccessor, aiService);

        var response = await sut.SetApiKey(
            new PreferenceSetAiKeyRequest("sk-test-1", Channel: "telegram", UserId: "mike"),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<PreferenceAiKeyStatusResponse>(ok.Value);

        Assert.Equal("openai", payload.Provider);
        Assert.True(payload.HasStoredKey);
        Assert.Equal("stored", payload.ApiKeySource);
        Assert.NotNull(aiService.LastSetApiKeyCall);
        Assert.Equal("sk-test-1", aiService.LastSetApiKeyCall!.ApiKey);
    }

    [Fact]
    public async Task RemoveApiKey_ShouldReturnUpdatedKeySource()
    {
        var scopeAccessor = new FakeConversationScopeAccessor();
        var aiService = new FakeAiRuntimeSettingsService
        {
            CurrentProvider = "openai",
            RuntimeApiKeySource = AiApiKeySource.Missing
        };
        var sut = new PreferenceAiController(scopeAccessor, aiService);

        var response = await sut.RemoveApiKey(provider: "openai", cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<PreferenceAiKeyStatusResponse>(ok.Value);

        Assert.Equal("openai", payload.Provider);
        Assert.False(payload.HasStoredKey);
        Assert.Equal("missing", payload.ApiKeySource);
        Assert.NotNull(aiService.LastRemoveApiKeyCall);
    }

    private sealed class FakeConversationScopeAccessor : IConversationScopeAccessor
    {
        public ConversationScope Current { get; private set; } = ConversationScope.Default;

        public void Set(ConversationScope scope)
        {
            Current = scope;
        }
    }

    private sealed class FakeAiRuntimeSettingsService : IAiRuntimeSettingsService
    {
        public IReadOnlyList<string> SupportedProviders { get; } = ["openai", "claude"];

        public string CurrentProvider { get; set; } = "openai";

        public string CurrentModel { get; set; } = "gpt-4.1-mini";

        public bool HasStoredKey { get; set; }

        public AiApiKeySource RuntimeApiKeySource { get; set; } = AiApiKeySource.Missing;

        public SetProviderCall? LastSetProviderCall { get; private set; }

        public SetModelCall? LastSetModelCall { get; private set; }

        public SetApiKeyCall? LastSetApiKeyCall { get; private set; }

        public RemoveApiKeyCall? LastRemoveApiKeyCall { get; private set; }

        public bool TryNormalizeProvider(string? value, out string provider)
        {
            provider = "openai";
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var normalized = value.Trim().ToLowerInvariant();
            if (normalized is "open ai")
            {
                normalized = "openai";
            }
            else if (normalized is "anthropic")
            {
                normalized = "claude";
            }

            if (normalized is "openai" or "claude")
            {
                provider = normalized;
                return true;
            }

            return false;
        }

        public IReadOnlyList<string> GetSupportedModels(string provider)
        {
            return provider switch
            {
                "claude" => ["claude-3-7-sonnet-latest", "claude-3-5-haiku-latest"],
                _ => ["gpt-4.1-mini", "gpt-4.1"]
            };
        }

        public Task<string> GetProviderAsync(ConversationScope scope, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CurrentProvider);
        }

        public Task<string> SetProviderAsync(ConversationScope scope, string provider, CancellationToken cancellationToken = default)
        {
            CurrentProvider = provider;
            LastSetProviderCall = new SetProviderCall(scope, provider);
            return Task.FromResult(provider);
        }

        public Task<string> GetModelAsync(ConversationScope scope, string provider, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CurrentModel);
        }

        public Task<string> SetModelAsync(ConversationScope scope, string provider, string model, CancellationToken cancellationToken = default)
        {
            CurrentProvider = provider;
            CurrentModel = model.Trim();
            LastSetModelCall = new SetModelCall(scope, provider, CurrentModel);
            return Task.FromResult(CurrentModel);
        }

        public Task<bool> HasStoredApiKeyAsync(ConversationScope scope, string provider, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(HasStoredKey);
        }

        public Task SetApiKeyAsync(ConversationScope scope, string provider, string apiKey, CancellationToken cancellationToken = default)
        {
            LastSetApiKeyCall = new SetApiKeyCall(scope, provider, apiKey);
            HasStoredKey = true;
            RuntimeApiKeySource = AiApiKeySource.Stored;
            return Task.CompletedTask;
        }

        public Task RemoveApiKeyAsync(ConversationScope scope, string provider, CancellationToken cancellationToken = default)
        {
            LastRemoveApiKeyCall = new RemoveApiKeyCall(scope, provider);
            HasStoredKey = false;
            return Task.CompletedTask;
        }

        public Task<AiRuntimeSettings> ResolveAsync(ConversationScope scope, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AiRuntimeSettings(CurrentProvider, CurrentModel, string.Empty, RuntimeApiKeySource));
        }
    }

    private sealed record SetProviderCall(ConversationScope Scope, string Provider);

    private sealed record SetModelCall(ConversationScope Scope, string Provider, string Model);

    private sealed record SetApiKeyCall(ConversationScope Scope, string Provider, string ApiKey);

    private sealed record RemoveApiKeyCall(ConversationScope Scope, string Provider);
}
