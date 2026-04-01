namespace LagerthaAssistant.Api.Controllers;

using LagerthaAssistant.Api.Contracts;
using LagerthaAssistant.Application.Interfaces.Common;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/preferences/ai")]
public sealed class PreferenceAiController : ControllerBase
{
    private readonly IConversationScopeAccessor _scopeAccessor;
    private readonly IAiRuntimeSettingsService _aiRuntimeSettingsService;

    public PreferenceAiController(
        IConversationScopeAccessor scopeAccessor,
        IAiRuntimeSettingsService aiRuntimeSettingsService)
    {
        _scopeAccessor = scopeAccessor;
        _aiRuntimeSettingsService = aiRuntimeSettingsService;
    }

    [HttpGet("provider")]
    [ProducesResponseType(typeof(PreferenceAiProviderResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PreferenceAiProviderResponse>> GetProvider(
        [FromQuery] string? channel = null,
        [FromQuery] string? userId = null,
        [FromQuery] string? conversationId = null,
        CancellationToken cancellationToken = default)
    {
        var scope = ApiConversationScopeApplier.Apply(_scopeAccessor, channel, userId, conversationId);
        var provider = await _aiRuntimeSettingsService.GetProviderAsync(scope, cancellationToken);

        return Ok(new PreferenceAiProviderResponse(provider, _aiRuntimeSettingsService.SupportedProviders));
    }

    [HttpPut("provider")]
    [ProducesResponseType(typeof(PreferenceAiProviderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PreferenceAiProviderResponse>> SetProvider(
        [FromBody] PreferenceSetAiProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Provider))
        {
            return BadRequest("Provider is required.");
        }

        if (!_aiRuntimeSettingsService.TryNormalizeProvider(request.Provider, out var provider))
        {
            return BadRequest(ApiModeValidationErrors.BuildUnsupported(
                "provider",
                request.Provider,
                _aiRuntimeSettingsService.SupportedProviders));
        }

        var scope = ApiConversationScopeApplier.Apply(_scopeAccessor, request.Channel, request.UserId, request.ConversationId);
        var persistedProvider = await _aiRuntimeSettingsService.SetProviderAsync(scope, provider, cancellationToken);

        return Ok(new PreferenceAiProviderResponse(persistedProvider, _aiRuntimeSettingsService.SupportedProviders));
    }

    [HttpGet("model")]
    [ProducesResponseType(typeof(PreferenceAiModelResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PreferenceAiModelResponse>> GetModel(
        [FromQuery] string? provider = null,
        [FromQuery] string? channel = null,
        [FromQuery] string? userId = null,
        [FromQuery] string? conversationId = null,
        CancellationToken cancellationToken = default)
    {
        var scope = ApiConversationScopeApplier.Apply(_scopeAccessor, channel, userId, conversationId);
        var resolvedProviderResult = await ResolveProviderAsync(scope, provider, cancellationToken);
        if (!resolvedProviderResult.IsResolved)
        {
            return BadRequest(resolvedProviderResult.ErrorMessage);
        }

        var model = await _aiRuntimeSettingsService.GetModelAsync(scope, resolvedProviderResult.Provider, cancellationToken);
        var models = _aiRuntimeSettingsService.GetSupportedModels(resolvedProviderResult.Provider);
        return Ok(new PreferenceAiModelResponse(resolvedProviderResult.Provider, model, models));
    }

    [HttpPut("model")]
    [ProducesResponseType(typeof(PreferenceAiModelResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PreferenceAiModelResponse>> SetModel(
        [FromBody] PreferenceSetAiModelRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Model))
        {
            return BadRequest("Model is required.");
        }

        var scope = ApiConversationScopeApplier.Apply(_scopeAccessor, request.Channel, request.UserId, request.ConversationId);
        var resolvedProviderResult = await ResolveProviderAsync(scope, request.Provider, cancellationToken);
        if (!resolvedProviderResult.IsResolved)
        {
            return BadRequest(resolvedProviderResult.ErrorMessage);
        }

        var model = await _aiRuntimeSettingsService.SetModelAsync(
            scope,
            resolvedProviderResult.Provider,
            request.Model,
            cancellationToken);

        return Ok(new PreferenceAiModelResponse(
            resolvedProviderResult.Provider,
            model,
            _aiRuntimeSettingsService.GetSupportedModels(resolvedProviderResult.Provider)));
    }

    [HttpGet("key/status")]
    [ProducesResponseType(typeof(PreferenceAiKeyStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PreferenceAiKeyStatusResponse>> GetKeyStatus(
        [FromQuery] string? provider = null,
        [FromQuery] string? channel = null,
        [FromQuery] string? userId = null,
        [FromQuery] string? conversationId = null,
        CancellationToken cancellationToken = default)
    {
        var scope = ApiConversationScopeApplier.Apply(_scopeAccessor, channel, userId, conversationId);
        var resolvedProviderResult = await ResolveProviderAsync(scope, provider, cancellationToken);
        if (!resolvedProviderResult.IsResolved)
        {
            return BadRequest(resolvedProviderResult.ErrorMessage);
        }

        var hasStoredKey = await _aiRuntimeSettingsService.HasStoredApiKeyAsync(
            scope,
            resolvedProviderResult.Provider,
            cancellationToken);

        var source = hasStoredKey
            ? "stored"
            : await ResolveApiKeySourceAsync(scope, resolvedProviderResult.Provider, cancellationToken);

        return Ok(new PreferenceAiKeyStatusResponse(
            resolvedProviderResult.Provider,
            hasStoredKey,
            source));
    }

    [HttpPost("key")]
    [ProducesResponseType(typeof(PreferenceAiKeyStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PreferenceAiKeyStatusResponse>> SetApiKey(
        [FromBody] PreferenceSetAiKeyRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.ApiKey))
        {
            return BadRequest("ApiKey is required.");
        }

        var scope = ApiConversationScopeApplier.Apply(_scopeAccessor, request.Channel, request.UserId, request.ConversationId);
        var resolvedProviderResult = await ResolveProviderAsync(scope, request.Provider, cancellationToken);
        if (!resolvedProviderResult.IsResolved)
        {
            return BadRequest(resolvedProviderResult.ErrorMessage);
        }

        await _aiRuntimeSettingsService.SetApiKeyAsync(
            scope,
            resolvedProviderResult.Provider,
            request.ApiKey,
            cancellationToken);

        return Ok(new PreferenceAiKeyStatusResponse(
            resolvedProviderResult.Provider,
            HasStoredKey: true,
            ApiKeySource: "stored"));
    }

    [HttpDelete("key")]
    [ProducesResponseType(typeof(PreferenceAiKeyStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PreferenceAiKeyStatusResponse>> RemoveApiKey(
        [FromQuery] string? provider = null,
        [FromQuery] string? channel = null,
        [FromQuery] string? userId = null,
        [FromQuery] string? conversationId = null,
        CancellationToken cancellationToken = default)
    {
        var scope = ApiConversationScopeApplier.Apply(_scopeAccessor, channel, userId, conversationId);
        var resolvedProviderResult = await ResolveProviderAsync(scope, provider, cancellationToken);
        if (!resolvedProviderResult.IsResolved)
        {
            return BadRequest(resolvedProviderResult.ErrorMessage);
        }

        await _aiRuntimeSettingsService.RemoveApiKeyAsync(
            scope,
            resolvedProviderResult.Provider,
            cancellationToken);

        return Ok(new PreferenceAiKeyStatusResponse(
            resolvedProviderResult.Provider,
            HasStoredKey: false,
            ApiKeySource: await ResolveApiKeySourceAsync(scope, resolvedProviderResult.Provider, cancellationToken)));
    }

    private async Task<ResolvedProviderResult> ResolveProviderAsync(
        ConversationScope scope,
        string? provider,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            var current = await _aiRuntimeSettingsService.GetProviderAsync(scope, cancellationToken);
            return ResolvedProviderResult.Ok(current);
        }

        if (!_aiRuntimeSettingsService.TryNormalizeProvider(provider, out var normalized))
        {
            return ResolvedProviderResult.Fail(ApiModeValidationErrors.BuildUnsupported(
                "provider",
                provider,
                _aiRuntimeSettingsService.SupportedProviders));
        }

        return ResolvedProviderResult.Ok(normalized);
    }

    private async Task<string> ResolveApiKeySourceAsync(
        ConversationScope scope,
        string provider,
        CancellationToken cancellationToken)
    {
        var currentProvider = await _aiRuntimeSettingsService.GetProviderAsync(scope, cancellationToken);
        if (!string.Equals(currentProvider, provider, StringComparison.Ordinal))
        {
            // ResolveAsync is provider-bound to currently selected provider.
            // For non-selected providers in v1 we can safely report "missing" when no stored key exists.
            return "missing";
        }

        var runtime = await _aiRuntimeSettingsService.ResolveAsync(scope, cancellationToken);
        return runtime.ApiKeySource.ToString().ToLowerInvariant();
    }

    private sealed record ResolvedProviderResult(
        bool IsResolved,
        string Provider,
        string ErrorMessage)
    {
        public static ResolvedProviderResult Ok(string provider) => new(true, provider, string.Empty);

        public static ResolvedProviderResult Fail(string errorMessage) => new(false, string.Empty, errorMessage);
    }
}
