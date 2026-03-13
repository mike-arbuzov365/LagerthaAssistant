using LagerthaAssistant.Api.Contracts;
using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Models.Vocabulary;
using Microsoft.AspNetCore.Mvc;

namespace LagerthaAssistant.Api.Controllers;

[ApiController]
[Route("api/preferences")]
public sealed class PreferencesController : ControllerBase
{
    private const string DefaultChannel = "api";

    private readonly IConversationScopeAccessor _scopeAccessor;
    private readonly IVocabularySaveModePreferenceService _saveModePreferenceService;

    public PreferencesController(
        IConversationScopeAccessor scopeAccessor,
        IVocabularySaveModePreferenceService saveModePreferenceService)
    {
        _scopeAccessor = scopeAccessor;
        _saveModePreferenceService = saveModePreferenceService;
    }

    [HttpGet("save-mode")]
    [ProducesResponseType(typeof(PreferenceSaveModeResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PreferenceSaveModeResponse>> GetSaveMode(
        [FromQuery] string? channel = null,
        [FromQuery] string? userId = null,
        [FromQuery] string? conversationId = null,
        CancellationToken cancellationToken = default)
    {
        var scope = BuildScope(channel, userId, conversationId);
        _scopeAccessor.Set(scope);

        var mode = await _saveModePreferenceService.GetModeAsync(scope, cancellationToken);
        return Ok(BuildSaveModeResponse(mode));
    }

    [HttpPut("save-mode")]
    [ProducesResponseType(typeof(PreferenceSaveModeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PreferenceSaveModeResponse>> SetSaveMode(
        [FromBody] PreferenceSetSaveModeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Mode))
        {
            return BadRequest("Mode is required.");
        }

        if (!_saveModePreferenceService.TryParse(request.Mode, out var mode))
        {
            return BadRequest($"Unsupported mode '{request.Mode}'. Use ask, auto, or off.");
        }

        var scope = BuildScope(request.Channel, request.UserId, request.ConversationId);
        _scopeAccessor.Set(scope);

        await _saveModePreferenceService.SetModeAsync(scope, mode, cancellationToken);
        return Ok(BuildSaveModeResponse(mode));
    }

    private PreferenceSaveModeResponse BuildSaveModeResponse(VocabularySaveMode mode)
    {
        return new PreferenceSaveModeResponse(
            _saveModePreferenceService.ToText(mode),
            _saveModePreferenceService.SupportedModes);
    }

    private static ConversationScope BuildScope(string? channel, string? userId, string? conversationId)
    {
        var normalizedChannel = channel?.Trim().ToLowerInvariant();
        var effectiveChannel = string.IsNullOrWhiteSpace(normalizedChannel)
            ? DefaultChannel
            : normalizedChannel;

        return ConversationScope.Create(effectiveChannel, userId, conversationId);
    }
}
