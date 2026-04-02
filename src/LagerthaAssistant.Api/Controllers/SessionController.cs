using System.Text.Json;
using LagerthaAssistant.Api.Contracts;
using LagerthaAssistant.Api.Options;
using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.Interfaces;
using LagerthaAssistant.Application.Interfaces.Agents;
using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Interfaces.Food;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Infrastructure.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SharedBotKernel.Infrastructure.AI;
using SharedBotKernel.Infrastructure.Telegram;
using SharedBotKernel.Options;

namespace LagerthaAssistant.Api.Controllers;

[ApiController]
[Route("api/session")]
public sealed class SessionController : ControllerBase
{
    private static readonly TimeSpan MaxInitDataAge = TimeSpan.FromHours(24);

    private readonly IConversationScopeAccessor _scopeAccessor;
    private readonly IConversationBootstrapService _conversationBootstrapService;
    private readonly IUserLocaleStateService _localeStateService;
    private readonly IAiRuntimeSettingsService _aiRuntimeSettingsService;
    private readonly INotionSyncProcessor _notionSyncProcessor;
    private readonly IFoodSyncService _foodSyncService;
    private readonly TelegramOptions _telegramOptions;
    private readonly NotionFoodOptions _notionFoodOptions;
    private readonly NotionSyncWorkerOptions _notionSyncWorkerOptions;
    private readonly FoodSyncWorkerOptions _foodSyncWorkerOptions;

    public SessionController(
        IConversationScopeAccessor scopeAccessor,
        IConversationBootstrapService conversationBootstrapService,
        IUserLocaleStateService localeStateService,
        IAiRuntimeSettingsService aiRuntimeSettingsService,
        INotionSyncProcessor notionSyncProcessor,
        IFoodSyncService foodSyncService,
        IOptions<TelegramOptions> telegramOptions,
        NotionFoodOptions notionFoodOptions,
        IOptions<NotionSyncWorkerOptions> notionSyncWorkerOptions,
        IOptions<FoodSyncWorkerOptions> foodSyncWorkerOptions)
    {
        _scopeAccessor = scopeAccessor;
        _conversationBootstrapService = conversationBootstrapService;
        _localeStateService = localeStateService;
        _aiRuntimeSettingsService = aiRuntimeSettingsService;
        _notionSyncProcessor = notionSyncProcessor;
        _foodSyncService = foodSyncService;
        _telegramOptions = telegramOptions.Value;
        _notionFoodOptions = notionFoodOptions;
        _notionSyncWorkerOptions = notionSyncWorkerOptions.Value;
        _foodSyncWorkerOptions = foodSyncWorkerOptions.Value;
    }

    [HttpGet("bootstrap")]
    [ProducesResponseType(typeof(SessionBootstrapResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SessionBootstrapResponse>> GetBootstrap(
        [FromQuery] string? channel = null,
        [FromQuery] string? userId = null,
        [FromQuery] string? conversationId = null,
        [FromQuery] bool includeCommands = true,
        [FromQuery] bool includePartOfSpeechOptions = true,
        [FromQuery] bool includeDecks = false,
        [FromQuery] string? initData = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveScope(channel, userId, conversationId, initData, out var scope, out var scopeError))
        {
            return BadRequest(scopeError);
        }

        return await BuildBootstrapResponseAsync(
            scope,
            includeCommands,
            includePartOfSpeechOptions,
            includeDecks,
            cancellationToken);
    }

    [HttpPost("bootstrap")]
    [ProducesResponseType(typeof(SessionBootstrapResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SessionBootstrapResponse>> PostBootstrap(
        [FromBody] SessionBootstrapRequest? request,
        CancellationToken cancellationToken = default)
    {
        request ??= new SessionBootstrapRequest();

        if (!TryResolveScope(request.Channel, request.UserId, request.ConversationId, request.InitData, out var scope, out var scopeError))
        {
            return BadRequest(scopeError);
        }

        return await BuildBootstrapResponseAsync(
            scope,
            request.IncludeCommands,
            request.IncludePartOfSpeechOptions,
            request.IncludeDecks,
            cancellationToken);
    }

    private async Task<ActionResult<SessionBootstrapResponse>> BuildBootstrapResponseAsync(
        ConversationScope scope,
        bool includeCommands,
        bool includePartOfSpeechOptions,
        bool includeDecks,
        CancellationToken cancellationToken)
    {
        var options = new ConversationBootstrapOptions(
            IncludeCommandGroups: includeCommands,
            IncludePartOfSpeechOptions: includePartOfSpeechOptions,
            IncludeWritableDecks: includeDecks);

        // Keep these reads sequential. In production both operations can traverse the same
        // scoped persistence graph, and parallelizing them risks transient EF/db re-entrancy.
        var bootstrap = await _conversationBootstrapService.BuildAsync(scope, options, cancellationToken);
        var storedLocale = await _localeStateService.GetStoredLocaleAsync(scope.Channel, scope.UserId, cancellationToken);
        var locale = string.IsNullOrWhiteSpace(storedLocale)
            ? LocalizationConstants.UkrainianLocale
            : LocalizationConstants.NormalizeLocaleCode(storedLocale);
        var preferences = new PreferenceSessionResponse(
            bootstrap.SaveMode,
            bootstrap.AvailableSaveModes,
            bootstrap.StorageMode,
            bootstrap.AvailableStorageModes);

        var settings = await BuildSettingsBootstrapAsync(scope, cancellationToken);

        return Ok(new SessionBootstrapResponse(
            new SessionScopeResponse(bootstrap.Scope.Channel, bootstrap.Scope.UserId, bootstrap.Scope.ConversationId),
            new PreferenceLocaleResponse(locale, [LocalizationConstants.UkrainianLocale, LocalizationConstants.EnglishLocale]),
            preferences,
            MiniAppPolicyPayloadFactory.Create(),
            new GraphAuthStatusResponse(
                bootstrap.Graph.IsConfigured,
                bootstrap.Graph.IsAuthenticated,
                bootstrap.Graph.Message,
                bootstrap.Graph.AccessTokenExpiresAtUtc),
            settings,
            ApiConversationCommandCatalogMapper.MapGroupedItems(bootstrap.CommandGroups),
            ApiVocabularyPartOfSpeechMapper.MapOptions(bootstrap.PartOfSpeechOptions),
            bootstrap.WritableDecks is null
                ? null
                : ApiVocabularyDeckMapper.MapDecks(bootstrap.WritableDecks)));
    }

    private async Task<MiniAppSettingsBootstrapResponse> BuildSettingsBootstrapAsync(
        ConversationScope scope,
        CancellationToken cancellationToken)
    {
        var availableProviders = _aiRuntimeSettingsService.SupportedProviders;
        var provider = availableProviders.FirstOrDefault(static x => !string.IsNullOrWhiteSpace(x)) ?? "openai";
        var hasStoredKey = false;
        var apiKeySource = "missing";

        try
        {
            provider = await _aiRuntimeSettingsService.GetProviderAsync(scope, cancellationToken);
            hasStoredKey = await _aiRuntimeSettingsService.HasStoredApiKeyAsync(scope, provider, cancellationToken);
            apiKeySource = hasStoredKey ? "stored" : "missing";
        }
        catch
        {
            // Best-effort bootstrap enrichment only. The settings screen can still load
            // and recover by refreshing provider-specific state later.
        }

        var availableModels = _aiRuntimeSettingsService.GetSupportedModels(provider);
        var model = availableModels.FirstOrDefault(static x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;

        try
        {
            model = await _aiRuntimeSettingsService.GetModelAsync(scope, provider, cancellationToken);
        }
        catch
        {
            // Fall back to the first supported model for resilient bootstrap.
        }

        var notionStatus = BuildFallbackNotionStatus();
        try
        {
            notionStatus = await BuildNotionStatusAsync(cancellationToken);
        }
        catch
        {
            // Best-effort bootstrap enrichment only.
        }

        return new MiniAppSettingsBootstrapResponse(
            provider,
            availableProviders,
            model,
            availableModels,
            hasStoredKey,
            apiKeySource,
            notionStatus);
    }

    private async Task<IntegrationNotionHubStatusResponse> BuildNotionStatusAsync(CancellationToken cancellationToken)
    {
        var notionStatus = await _notionSyncProcessor.GetStatusAsync(cancellationToken);
        var foodStatus = await _foodSyncService.GetSyncStatusAsync(cancellationToken);

        return new IntegrationNotionHubStatusResponse(
            new IntegrationNotionStatusResponse(
                notionStatus.Enabled,
                notionStatus.IsConfigured,
                _notionSyncWorkerOptions.Enabled,
                notionStatus.Message,
                notionStatus.PendingCards,
                notionStatus.FailedCards),
            new IntegrationFoodStatusResponse(
                _notionFoodOptions.Enabled,
                _notionFoodOptions.IsConfigured,
                _foodSyncWorkerOptions.Enabled,
                foodStatus.InventoryPendingOrFailed,
                foodStatus.InventoryPermanentlyFailed,
                foodStatus.GroceryPendingOrFailed,
                foodStatus.GroceryPermanentlyFailed));
    }

    private IntegrationNotionHubStatusResponse BuildFallbackNotionStatus()
    {
        return new IntegrationNotionHubStatusResponse(
            new IntegrationNotionStatusResponse(
                false,
                false,
                _notionSyncWorkerOptions.Enabled,
                "Unavailable",
                0,
                0),
            new IntegrationFoodStatusResponse(
                _notionFoodOptions.Enabled,
                _notionFoodOptions.IsConfigured,
                _foodSyncWorkerOptions.Enabled,
                0,
                0,
                0,
                0));
    }

    private bool TryResolveScope(
        string? channel,
        string? userId,
        string? conversationId,
        string? initData,
        out ConversationScope scope,
        out string? errorMessage)
    {
        scope = ConversationScope.Default;
        errorMessage = null;

        if (!string.Equals(channel, "telegram", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(initData))
        {
            scope = ApiConversationScopeApplier.Apply(_scopeAccessor, channel, userId, conversationId);
            return true;
        }

        var verification = TelegramMiniAppInitDataVerifier.Verify(
            initData,
            _telegramOptions.BotToken,
            DateTimeOffset.UtcNow,
            MaxInitDataAge);

        if (!verification.IsValid)
        {
            errorMessage = $"initData is invalid: {verification.Reason}.";
            return false;
        }

        if (!TryParseTelegramUserId(initData, out var verifiedUserId))
        {
            errorMessage = "initData user is missing.";
            return false;
        }

        var verifiedConversationId = ResolveVerifiedTelegramConversationId(conversationId, verifiedUserId);
        if (verifiedConversationId is null)
        {
            errorMessage = "conversationId does not match verified Telegram user.";
            return false;
        }

        scope = ApiConversationScopeApplier.Apply(_scopeAccessor, "telegram", verifiedUserId, verifiedConversationId);
        return true;
    }

    private static string? ResolveVerifiedTelegramConversationId(string? requestedConversationId, string verifiedUserId)
    {
        if (string.IsNullOrWhiteSpace(requestedConversationId)
            || string.Equals(requestedConversationId.Trim(), ConversationScope.DefaultConversationId, StringComparison.OrdinalIgnoreCase))
        {
            return verifiedUserId;
        }

        var normalizedConversationId = requestedConversationId.Trim().ToLowerInvariant();
        if (string.Equals(normalizedConversationId, verifiedUserId, StringComparison.Ordinal)
            || normalizedConversationId.StartsWith($"{verifiedUserId}:", StringComparison.Ordinal))
        {
            return normalizedConversationId;
        }

        return null;
    }

    private static bool TryParseTelegramUserId(string initData, out string userId)
    {
        userId = string.Empty;

        try
        {
            var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in initData.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var idx = pair.IndexOf('=');
                if (idx <= 0)
                {
                    continue;
                }

                var key = Uri.UnescapeDataString(pair[..idx]);
                var value = Uri.UnescapeDataString(pair[(idx + 1)..]);
                parameters[key] = value;
            }

            if (!parameters.TryGetValue("user", out var rawUser) || string.IsNullOrWhiteSpace(rawUser))
            {
                return false;
            }

            using var document = JsonDocument.Parse(rawUser);
            if (!document.RootElement.TryGetProperty("id", out var idElement))
            {
                return false;
            }

            userId = idElement.ValueKind switch
            {
                JsonValueKind.Number when idElement.TryGetInt64(out var numericId) => numericId.ToString(),
                JsonValueKind.String => idElement.GetString() ?? string.Empty,
                _ => string.Empty
            };

            return !string.IsNullOrWhiteSpace(userId);
        }
        catch
        {
            userId = string.Empty;
            return false;
        }
    }
}
