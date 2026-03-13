using Microsoft.AspNetCore.Mvc;
using LagerthaAssistant.Api.Contracts;
using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.AI;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.Application.Services.Vocabulary;

namespace LagerthaAssistant.Api.Controllers;

[ApiController]
[Route("api/vocabulary")]
public sealed class VocabularyController : ControllerBase
{
    private readonly IVocabularyWorkflowService _workflowService;
    private readonly IVocabularyPersistenceService _persistenceService;
    private readonly IVocabularyBatchInputService _batchInputService;
    private readonly IVocabularyDeckService _deckService;
    private readonly IVocabularyStorageModeProvider _storageModeProvider;
    private readonly IVocabularyStoragePreferenceService _storagePreferenceService;
    private readonly IConversationScopeAccessor _scopeAccessor;

    public VocabularyController(
        IVocabularyWorkflowService workflowService,
        IVocabularyPersistenceService persistenceService,
        IVocabularyBatchInputService batchInputService,
        IVocabularyDeckService deckService,
        IVocabularyStorageModeProvider storageModeProvider,
        IVocabularyStoragePreferenceService storagePreferenceService,
        IConversationScopeAccessor scopeAccessor)
    {
        _workflowService = workflowService;
        _persistenceService = persistenceService;
        _batchInputService = batchInputService;
        _deckService = deckService;
        _storageModeProvider = storageModeProvider;
        _storagePreferenceService = storagePreferenceService;
        _scopeAccessor = scopeAccessor;
    }

    [HttpPost("analyze")]
    [ProducesResponseType(typeof(VocabularyWorkflowItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<VocabularyWorkflowItemResponse>> Analyze(
        [FromBody] VocabularyAnalyzeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Input))
        {
            return BadRequest("Input is required.");
        }

        var scope = ApiConversationScopeApplier.Apply(_scopeAccessor, request.Channel, request.UserId, request.ConversationId);

        var applyMode = await ApiVocabularyStorageModeApplier.TryApplyAsync(
            _storageModeProvider,
            _storagePreferenceService,
            scope,
            request.StorageMode,
            cancellationToken);
        if (!applyMode.Success)
        {
            return BadRequest(applyMode.Error);
        }

        var result = await _workflowService.ProcessAsync(
            request.Input,
            request.ForcedDeckFileName,
            request.OverridePartOfSpeech,
            cancellationToken);

        return Ok(MapWorkflowItem(result));
    }

    [HttpPost("analyze-batch")]
    [ProducesResponseType(typeof(IReadOnlyList<VocabularyWorkflowItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<VocabularyWorkflowItemResponse>>> AnalyzeBatch(
        [FromBody] VocabularyAnalyzeBatchRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null || request.Inputs is null || request.Inputs.Count == 0)
        {
            return BadRequest("At least one input is required.");
        }

        var inputs = request.Inputs
            .Where(input => !string.IsNullOrWhiteSpace(input))
            .Select(input => input.Trim())
            .ToList();

        if (inputs.Count == 0)
        {
            return BadRequest("At least one non-empty input is required.");
        }

        var scope = ApiConversationScopeApplier.Apply(_scopeAccessor, request.Channel, request.UserId, request.ConversationId);

        var applyMode = await ApiVocabularyStorageModeApplier.TryApplyAsync(
            _storageModeProvider,
            _storagePreferenceService,
            scope,
            request.StorageMode,
            cancellationToken);
        if (!applyMode.Success)
        {
            return BadRequest(applyMode.Error);
        }

        var results = await _workflowService.ProcessBatchAsync(inputs, cancellationToken);
        return Ok(results.Select(MapWorkflowItem).ToList());
    }

    [HttpPost("parse-batch")]
    [ProducesResponseType(typeof(VocabularyParseBatchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<VocabularyParseBatchResponse> ParseBatch([FromBody] VocabularyParseBatchRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Input))
        {
            return BadRequest("Input is required.");
        }

        var parseResult = _batchInputService.Parse(request.Input, request.ApplySpaceSplit);

        return Ok(new VocabularyParseBatchResponse(
            parseResult.Items,
            parseResult.ShouldOfferSpaceSplit,
            parseResult.SpaceSplitCandidates,
            parseResult.SingleItemWithoutSeparators));
    }

    [HttpGet("storage-mode")]
    [ProducesResponseType(typeof(VocabularyStorageModeResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<VocabularyStorageModeResponse>> GetStorageMode(
        [FromQuery] string? channel = null,
        [FromQuery] string? userId = null,
        [FromQuery] string? conversationId = null,
        CancellationToken cancellationToken = default)
    {
        var scope = ApiConversationScopeApplier.Apply(_scopeAccessor, channel, userId, conversationId);

        var mode = await _storagePreferenceService.GetModeAsync(scope, cancellationToken);
        _storageModeProvider.SetMode(mode);

        return Ok(BuildStorageModeResponse(mode));
    }

    [HttpPut("storage-mode")]
    [ProducesResponseType(typeof(VocabularyStorageModeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<VocabularyStorageModeResponse>> SetStorageMode(
        [FromBody] VocabularySetStorageModeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Mode))
        {
            return BadRequest("Mode is required.");
        }

        if (!_storageModeProvider.TryParse(request.Mode, out var mode))
        {
            return BadRequest($"Unsupported mode '{request.Mode}'. Use local or graph.");
        }

        var scope = ApiConversationScopeApplier.Apply(_scopeAccessor, request.Channel, request.UserId, request.ConversationId);

        await _storagePreferenceService.SetModeAsync(scope, mode, cancellationToken);
        _storageModeProvider.SetMode(mode);

        return Ok(BuildStorageModeResponse(mode));
    }

    [HttpGet("decks")]
    [ProducesResponseType(typeof(VocabularyDeckCatalogResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<VocabularyDeckCatalogResponse>> GetDecks(
        [FromQuery] string? channel = null,
        [FromQuery] string? userId = null,
        [FromQuery] string? conversationId = null,
        [FromQuery] string? storageMode = null,
        CancellationToken cancellationToken = default)
    {
        var scope = ApiConversationScopeApplier.Apply(_scopeAccessor, channel, userId, conversationId);

        var applyMode = await ApiVocabularyStorageModeApplier.TryApplyAsync(
            _storageModeProvider,
            _storagePreferenceService,
            scope,
            storageMode,
            cancellationToken);
        if (!applyMode.Success)
        {
            return BadRequest(applyMode.Error);
        }

        var storageModeText = _storageModeProvider.ToText(_storageModeProvider.CurrentMode);
        var decks = await _deckService.GetWritableDeckFilesAsync(cancellationToken);
        var mappedDecks = ApiVocabularyDeckMapper.MapDecks(decks);

        return Ok(new VocabularyDeckCatalogResponse(storageModeText, mappedDecks));
    }

    [HttpGet("markers")]
    [ProducesResponseType(typeof(VocabularyPartOfSpeechCatalogResponse), StatusCodes.Status200OK)]
    public ActionResult<VocabularyPartOfSpeechCatalogResponse> GetPartOfSpeechMarkers()
    {
        var markers = ApiVocabularyPartOfSpeechMapper.BuildOptions();

        return Ok(new VocabularyPartOfSpeechCatalogResponse(markers));
    }

    [HttpPost("save-batch")]
    [ProducesResponseType(typeof(VocabularySaveBatchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<VocabularySaveBatchResponse>> SaveBatch(
        [FromBody] VocabularySaveBatchRequest request,
        [FromQuery] string? channel = null,
        [FromQuery] string? userId = null,
        [FromQuery] string? conversationId = null,
        [FromQuery] string? storageMode = null,
        CancellationToken cancellationToken = default)
    {
        if (request is null || request.Items is null || request.Items.Count == 0)
        {
            return BadRequest("At least one item is required.");
        }

        var scope = ApiConversationScopeApplier.Apply(_scopeAccessor, channel, userId, conversationId);

        var applyMode = await ApiVocabularyStorageModeApplier.TryApplyAsync(
            _storageModeProvider,
            _storagePreferenceService,
            scope,
            storageMode,
            cancellationToken);
        if (!applyMode.Success)
        {
            return BadRequest(applyMode.Error);
        }

        var items = request.Items.ToList();
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];

            if (string.IsNullOrWhiteSpace(item.RequestedWord))
            {
                return BadRequest($"items[{index}].requestedWord is required.");
            }

            if (string.IsNullOrWhiteSpace(item.AssistantReply))
            {
                return BadRequest($"items[{index}].assistantReply is required.");
            }
        }

        var responses = new List<VocabularySaveBatchItemResponse>(items.Count);
        var added = 0;
        var duplicates = 0;
        var failed = 0;

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];

            var appendResult = await _persistenceService.AppendFromAssistantReplyAsync(
                item.RequestedWord,
                item.AssistantReply,
                item.ForcedDeckFileName,
                item.OverridePartOfSpeech,
                cancellationToken);

            switch (appendResult.Status)
            {
                case VocabularyAppendStatus.Added:
                    added++;
                    break;
                case VocabularyAppendStatus.DuplicateFound:
                    duplicates++;
                    break;
                default:
                    failed++;
                    break;
            }

            responses.Add(new VocabularySaveBatchItemResponse(
                index + 1,
                item.RequestedWord,
                MapAppendResult(appendResult)));
        }

        return Ok(new VocabularySaveBatchResponse(
            items.Count,
            added,
            duplicates,
            failed,
            responses));
    }

    [HttpPost("save")]
    [ProducesResponseType(typeof(VocabularyAppendResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<VocabularyAppendResultResponse>> Save(
        [FromBody] VocabularySaveRequest request,
        [FromQuery] string? channel = null,
        [FromQuery] string? userId = null,
        [FromQuery] string? conversationId = null,
        [FromQuery] string? storageMode = null,
        CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.RequestedWord))
        {
            return BadRequest("RequestedWord is required.");
        }

        if (string.IsNullOrWhiteSpace(request.AssistantReply))
        {
            return BadRequest("AssistantReply is required.");
        }

        var scope = ApiConversationScopeApplier.Apply(_scopeAccessor, channel, userId, conversationId);

        var applyMode = await ApiVocabularyStorageModeApplier.TryApplyAsync(
            _storageModeProvider,
            _storagePreferenceService,
            scope,
            storageMode,
            cancellationToken);
        if (!applyMode.Success)
        {
            return BadRequest(applyMode.Error);
        }

        var result = await _persistenceService.AppendFromAssistantReplyAsync(
            request.RequestedWord,
            request.AssistantReply,
            request.ForcedDeckFileName,
            request.OverridePartOfSpeech,
            cancellationToken);

        return Ok(MapAppendResult(result));
    }

    private VocabularyStorageModeResponse BuildStorageModeResponse(VocabularyStorageMode mode)
    {
        var modeText = _storageModeProvider.ToText(mode);
        return new VocabularyStorageModeResponse(modeText, _storagePreferenceService.SupportedModes);
    }

    private static VocabularyWorkflowItemResponse MapWorkflowItem(VocabularyWorkflowItemResult result)
    {
        return new VocabularyWorkflowItemResponse(
            result.Input,
            result.FoundInDeck,
            MapLookup(result.Lookup),
            MapCompletion(result.AssistantCompletion),
            MapPreview(result.AppendPreview));
    }

    private static VocabularyLookupResponse MapLookup(VocabularyLookupResult lookup)
    {
        return new VocabularyLookupResponse(
            lookup.Query,
            lookup.Found,
            lookup.Matches.Select(MapDeckEntry).ToList());
    }

    private static VocabularyAssistantCompletionResponse? MapCompletion(AssistantCompletionResult? completion)
    {
        if (completion is null)
        {
            return null;
        }

        var usage = completion.Usage is null
            ? null
            : new VocabularyAssistantUsageResponse(
                completion.Usage.PromptTokens,
                completion.Usage.CompletionTokens,
                completion.Usage.TotalTokens);

        return new VocabularyAssistantCompletionResponse(
            completion.Content,
            completion.Model,
            usage);
    }

    private static VocabularyAppendPreviewResponse? MapPreview(VocabularyAppendPreviewResult? preview)
    {
        if (preview is null)
        {
            return null;
        }

        return new VocabularyAppendPreviewResponse(
            preview.Status.ToString().ToLowerInvariant(),
            preview.Word,
            preview.TargetDeckFileName,
            preview.TargetDeckPath,
            preview.DuplicateMatches?.Select(MapDeckEntry).ToList(),
            preview.Message);
    }

    private static VocabularyAppendResultResponse MapAppendResult(VocabularyAppendResult result)
    {
        return new VocabularyAppendResultResponse(
            result.Status.ToString().ToLowerInvariant(),
            result.Entry is null ? null : MapDeckEntry(result.Entry),
            result.DuplicateMatches?.Select(MapDeckEntry).ToList(),
            result.Message);
    }

    private static VocabularyDeckEntryResponse MapDeckEntry(VocabularyDeckEntry entry)
    {
        return new VocabularyDeckEntryResponse(
            entry.DeckFileName,
            entry.DeckPath,
            entry.RowNumber,
            entry.Word,
            entry.Meaning,
            entry.Examples);
    }

}







