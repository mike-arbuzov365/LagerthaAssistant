using Microsoft.AspNetCore.Mvc;
using LagerthaAssistant.Api.Contracts;
using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Agents;

namespace LagerthaAssistant.Api.Controllers;

[ApiController]
[Route("api/session")]
public sealed class SessionController : ControllerBase
{
    private readonly IConversationScopeAccessor _scopeAccessor;
    private readonly IVocabularySessionPreferenceService _sessionPreferenceService;
    private readonly IVocabularySaveModePreferenceService _saveModePreferenceService;
    private readonly IVocabularyStorageModeProvider _storageModeProvider;
    private readonly IGraphAuthService _graphAuthService;

    public SessionController(
        IConversationScopeAccessor scopeAccessor,
        IVocabularySessionPreferenceService sessionPreferenceService,
        IVocabularySaveModePreferenceService saveModePreferenceService,
        IVocabularyStorageModeProvider storageModeProvider,
        IGraphAuthService graphAuthService)
    {
        _scopeAccessor = scopeAccessor;
        _sessionPreferenceService = sessionPreferenceService;
        _saveModePreferenceService = saveModePreferenceService;
        _storageModeProvider = storageModeProvider;
        _graphAuthService = graphAuthService;
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

        var session = await _sessionPreferenceService.GetAsync(scope, cancellationToken);
        var graph = await _graphAuthService.GetStatusAsync(cancellationToken);

        var preferences = new PreferenceSessionResponse(
            _saveModePreferenceService.ToText(session.SaveMode),
            _sessionPreferenceService.SupportedSaveModes,
            _storageModeProvider.ToText(session.StorageMode),
            _sessionPreferenceService.SupportedStorageModes);

        return Ok(new SessionBootstrapResponse(
            new SessionScopeResponse(scope.Channel, scope.UserId, scope.ConversationId),
            preferences,
            new GraphAuthStatusResponse(
                graph.IsConfigured,
                graph.IsAuthenticated,
                graph.Message,
                graph.AccessTokenExpiresAtUtc),
            ApiConversationCommandCatalogMapper.BuildGroupedItems()));
    }
}
