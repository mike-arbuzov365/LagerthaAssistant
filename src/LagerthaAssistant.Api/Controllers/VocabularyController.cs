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
    private readonly IVocabularyBatchInputService _batchInputService;
    private readonly IConversationScopeAccessor _scopeAccessor;

    public VocabularyController(
        IVocabularyWorkflowService workflowService,
        IVocabularyPersistenceService persistenceService,
        IVocabularyBatchInputService batchInputService,
        IConversationScopeAccessor scopeAccessor)
    {
        _workflowService = workflowService;
        _persistenceService = persistenceService;
        _batchInputService = batchInputService;
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

    [HttpPost("save-batch")]
    [ProducesResponseType(typeof(VocabularySaveBatchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<VocabularySaveBatchResponse>> SaveBatch(
        [FromBody] VocabularySaveBatchRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null || request.Items is null || request.Items.Count == 0)
        {
            return BadRequest("At least one item is required.");
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
