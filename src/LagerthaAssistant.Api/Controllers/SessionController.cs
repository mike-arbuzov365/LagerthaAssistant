using Microsoft.AspNetCore.Mvc;
using LagerthaAssistant.Api.Contracts;
using LagerthaAssistant.Application.Interfaces.Agents;
using LagerthaAssistant.Application.Interfaces.Common;

namespace LagerthaAssistant.Api.Controllers;

[ApiController]
[Route("api/session")]
public sealed class SessionController : ControllerBase
{
    private readonly IConversationScopeAccessor _scopeAccessor;
    private readonly IConversationBootstrapService _conversationBootstrapService;

    public SessionController(
        IConversationScopeAccessor scopeAccessor,
        IConversationBootstrapService conversationBootstrapService)
    {
        _scopeAccessor = scopeAccessor;
        _conversationBootstrapService = conversationBootstrapService;
    }

    [HttpGet("bootstrap")]
    [ProducesResponseType(typeof(SessionBootstrapResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SessionBootstrapResponse>> GetBootstrap(
        [FromQuery] string? channel = null,
        [FromQuery] string? userId = null,
        [FromQuery] string? conversationId = null,
        CancellationToken cancellationToken = default)
    {
        var scope = ApiConversationScopeBuilder.Build(channel, userId, conversationId);
        _scopeAccessor.Set(scope);

        var bootstrap = await _conversationBootstrapService.BuildAsync(scope, cancellationToken);

        var preferences = new PreferenceSessionResponse(
            bootstrap.SaveMode,
            bootstrap.AvailableSaveModes,
            bootstrap.StorageMode,
            bootstrap.AvailableStorageModes);

        return Ok(new SessionBootstrapResponse(
            new SessionScopeResponse(bootstrap.Scope.Channel, bootstrap.Scope.UserId, bootstrap.Scope.ConversationId),
            preferences,
            new GraphAuthStatusResponse(
                bootstrap.Graph.IsConfigured,
                bootstrap.Graph.IsAuthenticated,
                bootstrap.Graph.Message,
                bootstrap.Graph.AccessTokenExpiresAtUtc),
            ApiConversationCommandCatalogMapper.MapGroupedItems(bootstrap.CommandGroups),
            ApiVocabularyPartOfSpeechMapper.MapOptions(bootstrap.PartOfSpeechOptions)));
    }
}
