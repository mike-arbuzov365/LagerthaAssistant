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
    private readonly IVocabularySessionPreferenceService _sessionPreferenceService;
    private readonly IVocabularyStorageModeProvider _storageModeProvider;

    public PreferencesController(
        IConversationScopeAccessor scopeAccessor,
        IVocabularySaveModePreferenceService saveModePreferenceService,
        IVocabularySessionPreferenceService sessionPreferenceService,
        IVocabularyStorageModeProvider storageModeProvider)
    {
        _scopeAccessor = scopeAccessor;
        _saveModePreferenceService = saveModePreferenceService;
        _sessionPreferenceService = sessionPreferenceService;
        _storageModeProvider = storageModeProvider;
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

    [HttpGet("session")]
    [ProducesResponseType(typeof(PreferenceSessionResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PreferenceSessionResponse>> GetSession(
        [FromQuery] string? channel = null,
        [FromQuery] string? userId = null,
        [FromQuery] string? conversationId = null,
        CancellationToken cancellationToken = default)
    {
        var scope = BuildScope(channel, userId, conversationId);
        _scopeAccessor.Set(scope);

        var session = await _sessionPreferenceService.GetAsync(scope, cancellationToken);
        _storageModeProvider.SetMode(session.StorageMode);

        return Ok(BuildSessionResponse(session.SaveMode, session.StorageMode));
    }

    [HttpPut("session")]
    [ProducesResponseType(typeof(PreferenceSessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PreferenceSessionResponse>> SetSession(
        [FromBody] PreferenceSetSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return BadRequest("Request body is required.");
        }

        var hasSaveMode = !string.IsNullOrWhiteSpace(request.SaveMode);
        var hasStorageMode = !string.IsNullOrWhiteSpace(request.StorageMode);
        if (!hasSaveMode && !hasStorageMode)
        {
            return BadRequest("At least one mode is required (saveMode or storageMode).");
        }

        VocabularySaveMode? parsedSaveMode = null;
        if (hasSaveMode)
        {
            if (!_saveModePreferenceService.TryParse(request.SaveMode, out var saveMode))
            {
                return BadRequest($"Unsupported save mode '{request.SaveMode}'. Use ask, auto, or off.");
            }

            parsedSaveMode = saveMode;
        }

        VocabularyStorageMode? parsedStorageMode = null;
        if (hasStorageMode)
        {
            if (!_storageModeProvider.TryParse(request.StorageMode, out var storageMode))
            {
                return BadRequest($"Unsupported storage mode '{request.StorageMode}'. Use local or graph.");
            }

            parsedStorageMode = storageMode;
        }

        var scope = BuildScope(request.Channel, request.UserId, request.ConversationId);
        _scopeAccessor.Set(scope);

        var session = await _sessionPreferenceService.SetAsync(scope, parsedSaveMode, parsedStorageMode, cancellationToken);
        _storageModeProvider.SetMode(session.StorageMode);

        return Ok(BuildSessionResponse(session.SaveMode, session.StorageMode));
    }

    private PreferenceSaveModeResponse BuildSaveModeResponse(VocabularySaveMode mode)
    {
        return new PreferenceSaveModeResponse(
            _saveModePreferenceService.ToText(mode),
            _saveModePreferenceService.SupportedModes);
    }

    private PreferenceSessionResponse BuildSessionResponse(
        VocabularySaveMode saveMode,
        VocabularyStorageMode storageMode)
    {
        return new PreferenceSessionResponse(
            _saveModePreferenceService.ToText(saveMode),
            _saveModePreferenceService.SupportedModes,
            _storageModeProvider.ToText(storageMode),
            _sessionPreferenceService.SupportedStorageModes);
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
