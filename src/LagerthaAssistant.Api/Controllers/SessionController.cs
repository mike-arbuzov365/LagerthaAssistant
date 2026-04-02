using Microsoft.AspNetCore.Mvc;
using LagerthaAssistant.Api.Contracts;
using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.Interfaces.Agents;
using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Interfaces;
using LagerthaAssistant.Application.Models.Agents;

namespace LagerthaAssistant.Api.Controllers;

[ApiController]
[Route("api/session")]
public sealed class SessionController : ControllerBase
{
    private readonly IConversationScopeAccessor _scopeAccessor;
    private readonly IConversationBootstrapService _conversationBootstrapService;
    private readonly IUserLocaleStateService _localeStateService;

    public SessionController(
        IConversationScopeAccessor scopeAccessor,
        IConversationBootstrapService conversationBootstrapService,
        IUserLocaleStateService localeStateService)
    {
        _scopeAccessor = scopeAccessor;
        _conversationBootstrapService = conversationBootstrapService;
        _localeStateService = localeStateService;
    }

    [HttpGet("bootstrap")]
    [ProducesResponseType(typeof(SessionBootstrapResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SessionBootstrapResponse>> GetBootstrap(
        [FromQuery] string? channel = null,
        [FromQuery] string? userId = null,
        [FromQuery] string? conversationId = null,
        [FromQuery] bool includeCommands = true,
        [FromQuery] bool includePartOfSpeechOptions = true,
        [FromQuery] bool includeDecks = false,
        CancellationToken cancellationToken = default)
    {
        var scope = ApiConversationScopeApplier.Apply(_scopeAccessor, channel, userId, conversationId);

        var options = new ConversationBootstrapOptions(
            IncludeCommandGroups: includeCommands,
            IncludePartOfSpeechOptions: includePartOfSpeechOptions,
            IncludeWritableDecks: includeDecks);

        var bootstrapTask = _conversationBootstrapService.BuildAsync(scope, options, cancellationToken);
        var storedLocaleTask = _localeStateService.GetStoredLocaleAsync(scope.Channel, scope.UserId, cancellationToken);
        await Task.WhenAll(bootstrapTask, storedLocaleTask);

        var bootstrap = await bootstrapTask;
        var storedLocale = await storedLocaleTask;
        var locale = string.IsNullOrWhiteSpace(storedLocale)
            ? LocalizationConstants.UkrainianLocale
            : LocalizationConstants.NormalizeLocaleCode(storedLocale);

        var preferences = new PreferenceSessionResponse(
            bootstrap.SaveMode,
            bootstrap.AvailableSaveModes,
            bootstrap.StorageMode,
            bootstrap.AvailableStorageModes);

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
            ApiConversationCommandCatalogMapper.MapGroupedItems(bootstrap.CommandGroups),
            ApiVocabularyPartOfSpeechMapper.MapOptions(bootstrap.PartOfSpeechOptions),
            bootstrap.WritableDecks is null
                ? null
                : ApiVocabularyDeckMapper.MapDecks(bootstrap.WritableDecks)));
    }
}
