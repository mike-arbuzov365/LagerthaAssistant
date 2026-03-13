using Microsoft.AspNetCore.Mvc;
using LagerthaAssistant.Api.Contracts;
using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.AI;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Models.Vocabulary;

namespace LagerthaAssistant.Api.Controllers;

[ApiController]
[Route("api/vocabulary")]
public sealed class VocabularyController : ControllerBase
{
    private const string DefaultChannel = "api";

    private readonly IVocabularyWorkflowService _workflowService;
    private readonly IVocabularyPersistenceService _persistenceService;
    private readonly IConversationScopeAccessor _scopeAccessor;

    public VocabularyController(
        IVocabularyWorkflowService workflowService,
        IVocabularyPersistenceService persistenceService,
        IConversationScopeAccessor scopeAccessor)
    {
        _workflowService = workflowService;
        _persistenceService = persistenceService;
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

        var scope = BuildScope(request.Channel, request.UserId, request.ConversationId);
        _scopeAccessor.Set(scope);

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

        var scope = BuildScope(request.Channel, request.UserId, request.ConversationId);
        _scopeAccessor.Set(scope);

        var results = await _workflowService.ProcessBatchAsync(inputs, cancellationToken);
        return Ok(results.Select(MapWorkflowItem).ToList());
    }

    [HttpPost("save")]
    [ProducesResponseType(typeof(VocabularyAppendResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<VocabularyAppendResultResponse>> Save(
        [FromBody] VocabularySaveRequest request,
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

        var result = await _persistenceService.AppendFromAssistantReplyAsync(
            request.RequestedWord,
            request.AssistantReply,
            request.ForcedDeckFileName,
            request.OverridePartOfSpeech,
            cancellationToken);

        return Ok(MapAppendResult(result));
    }

    private static ConversationScope BuildScope(string? channel, string? userId, string? conversationId)
    {
        var normalizedChannel = channel?.Trim().ToLowerInvariant();
        var effectiveChannel = string.IsNullOrWhiteSpace(normalizedChannel)
            ? DefaultChannel
            : normalizedChannel;

        return ConversationScope.Create(effectiveChannel, userId, conversationId);
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

