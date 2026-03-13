using Microsoft.AspNetCore.Mvc;
using LagerthaAssistant.Api.Contracts;
using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.Interfaces.AI;
using LagerthaAssistant.Application.Interfaces.Agents;
using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.Application.Services.Vocabulary;
using LagerthaAssistant.Domain.Entities;

namespace LagerthaAssistant.Api.Controllers;

[ApiController]
[Route("api/conversation")]
public sealed class ConversationController : ControllerBase
{
    private readonly IConversationOrchestrator _orchestrator;
    private readonly IAssistantSessionService _assistantSessionService;
    private readonly IConversationScopeAccessor _scopeAccessor;
    private readonly IVocabularyStorageModeProvider _storageModeProvider;
    private readonly IVocabularyStoragePreferenceService _storagePreferenceService;
    private readonly IConversationCommandCatalogService _commandCatalogService;

    public ConversationController(
        IConversationOrchestrator orchestrator,
        IAssistantSessionService assistantSessionService,
        IConversationScopeAccessor scopeAccessor,
        IVocabularyStorageModeProvider storageModeProvider,
        IVocabularyStoragePreferenceService storagePreferenceService,
        IConversationCommandCatalogService commandCatalogService)
    {
        _orchestrator = orchestrator;
        _assistantSessionService = assistantSessionService;
        _scopeAccessor = scopeAccessor;
        _storageModeProvider = storageModeProvider;
        _storagePreferenceService = storagePreferenceService;
        _commandCatalogService = commandCatalogService;
    }

    [HttpGet("commands")]
    [ProducesResponseType(typeof(IReadOnlyList<ConversationCommandItemResponse>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<ConversationCommandItemResponse>> GetCommands()
    {
        return Ok(ApiConversationCommandCatalogMapper.MapFlatItems(_commandCatalogService.GetCommands()));
    }

    [HttpGet("commands/grouped")]
    [ProducesResponseType(typeof(IReadOnlyList<ConversationCommandGroupResponse>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<ConversationCommandGroupResponse>> GetGroupedCommands()
    {
        return Ok(ApiConversationCommandCatalogMapper.MapGroupedItems(_commandCatalogService.GetGroups()));
    }

    [HttpGet("history")]
    [ProducesResponseType(typeof(IReadOnlyList<ConversationHistoryEntryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ConversationHistoryEntryResponse>>> GetHistory(
        [FromQuery] int take = 20,
        [FromQuery] string? channel = null,
        [FromQuery] string? userId = null,
        [FromQuery] string? conversationId = null,
        CancellationToken cancellationToken = default)
    {
        var scope = ApiConversationScopeApplier.Apply(_scopeAccessor, channel, userId, conversationId);

        var normalizedTake = Math.Max(1, take);
        var history = await _assistantSessionService.GetRecentHistoryAsync(normalizedTake, cancellationToken);

        var response = history
            .Select(item => new ConversationHistoryEntryResponse(
                item.Role.ToString().ToLowerInvariant(),
                item.Content,
                item.CreatedAtUtc))
            .ToList();

        return Ok(response);
    }

    [HttpGet("memory")]
    [ProducesResponseType(typeof(IReadOnlyList<ConversationMemoryEntryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ConversationMemoryEntryResponse>>> GetMemory(
        [FromQuery] int take = 20,
        [FromQuery] string? channel = null,
        [FromQuery] string? userId = null,
        [FromQuery] string? conversationId = null,
        CancellationToken cancellationToken = default)
    {
        var scope = ApiConversationScopeApplier.Apply(_scopeAccessor, channel, userId, conversationId);

        var normalizedTake = Math.Max(1, take);
        var memory = await _assistantSessionService.GetActiveMemoryAsync(normalizedTake, cancellationToken);

        var response = memory
            .Select(item => new ConversationMemoryEntryResponse(
                item.Key,
                item.Value,
                item.Confidence,
                item.IsActive,
                item.LastSeenAtUtc))
            .ToList();

        return Ok(response);
    }

    [HttpGet("prompt")]
    [ProducesResponseType(typeof(ConversationSystemPromptResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ConversationSystemPromptResponse>> GetPrompt(
        CancellationToken cancellationToken = default)
    {
        var prompt = await _assistantSessionService.GetSystemPromptAsync(cancellationToken);
        return Ok(new ConversationSystemPromptResponse(prompt));
    }

    [HttpPut("prompt")]
    [ProducesResponseType(typeof(ConversationSystemPromptResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ConversationSystemPromptResponse>> SetPrompt(
        [FromBody] ConversationSetSystemPromptRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Prompt))
        {
            return BadRequest("Prompt is required.");
        }

        var source = string.IsNullOrWhiteSpace(request.Source)
            ? "manual"
            : request.Source.Trim();

        var updatedPrompt = await _assistantSessionService.SetSystemPromptAsync(request.Prompt, source, cancellationToken);
        return Ok(new ConversationSystemPromptResponse(updatedPrompt));
    }

    [HttpPost("prompt/default")]
    [ProducesResponseType(typeof(ConversationSystemPromptResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ConversationSystemPromptResponse>> ResetPromptToDefault(
        CancellationToken cancellationToken = default)
    {
        var updatedPrompt = await _assistantSessionService.SetSystemPromptAsync(AssistantDefaults.SystemPrompt, "default", cancellationToken);
        return Ok(new ConversationSystemPromptResponse(updatedPrompt));
    }

    [HttpGet("prompt/history")]
    [ProducesResponseType(typeof(IReadOnlyList<ConversationSystemPromptHistoryEntryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ConversationSystemPromptHistoryEntryResponse>>> GetPromptHistory(
        [FromQuery] int take = 20,
        CancellationToken cancellationToken = default)
    {
        var normalizedTake = Math.Max(1, take);
        var history = await _assistantSessionService.GetSystemPromptHistoryAsync(normalizedTake, cancellationToken);

        var response = history
            .Select(MapPromptHistoryEntry)
            .ToList();

        return Ok(response);
    }

    [HttpGet("prompt/proposals")]
    [ProducesResponseType(typeof(IReadOnlyList<ConversationSystemPromptProposalResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ConversationSystemPromptProposalResponse>>> GetPromptProposals(
        [FromQuery] int take = 20,
        CancellationToken cancellationToken = default)
    {
        var normalizedTake = Math.Max(1, take);
        var proposals = await _assistantSessionService.GetSystemPromptProposalsAsync(normalizedTake, cancellationToken);

        var response = proposals
            .Select(MapPromptProposal)
            .ToList();

        return Ok(response);
    }

    [HttpPost("prompt/proposals")]
    [ProducesResponseType(typeof(ConversationSystemPromptProposalResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ConversationSystemPromptProposalResponse>> CreatePromptProposal(
        [FromBody] ConversationCreatePromptProposalRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Prompt))
        {
            return BadRequest("Proposed prompt is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return BadRequest("Proposal reason is required.");
        }

        var confidence = request.Confidence ?? 0.8;
        var source = string.IsNullOrWhiteSpace(request.Source)
            ? "manual"
            : request.Source.Trim();

        var proposal = await _assistantSessionService.CreateSystemPromptProposalAsync(
            request.Prompt,
            request.Reason,
            confidence,
            source,
            cancellationToken);

        return Ok(MapPromptProposal(proposal));
    }

    [HttpPost("prompt/proposals/improve")]
    [ProducesResponseType(typeof(ConversationSystemPromptProposalResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ConversationSystemPromptProposalResponse>> ImprovePromptProposal(
        [FromBody] ConversationPromptImproveRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Goal))
        {
            return BadRequest("Goal is required.");
        }

        var proposal = await _assistantSessionService.GenerateSystemPromptProposalAsync(request.Goal, cancellationToken);
        return Ok(MapPromptProposal(proposal));
    }

    [HttpPost("prompt/proposals/{proposalId:int}/apply")]
    [ProducesResponseType(typeof(ConversationSystemPromptResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConversationSystemPromptResponse>> ApplyPromptProposal(
        [FromRoute] int proposalId,
        CancellationToken cancellationToken = default)
    {
        if (proposalId <= 0)
        {
            return BadRequest("Proposal id must be greater than 0.");
        }

        try
        {
            var updatedPrompt = await _assistantSessionService.ApplySystemPromptProposalAsync(proposalId, cancellationToken);
            return Ok(new ConversationSystemPromptResponse(updatedPrompt));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPost("prompt/proposals/{proposalId:int}/reject")]
    [ProducesResponseType(typeof(ConversationActionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConversationActionResponse>> RejectPromptProposal(
        [FromRoute] int proposalId,
        CancellationToken cancellationToken = default)
    {
        if (proposalId <= 0)
        {
            return BadRequest("Proposal id must be greater than 0.");
        }

        try
        {
            await _assistantSessionService.RejectSystemPromptProposalAsync(proposalId, cancellationToken);
            return Ok(new ConversationActionResponse($"Proposal #{proposalId} rejected."));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpPost("reset")]
    [ProducesResponseType(typeof(ConversationActionResponse), StatusCodes.Status200OK)]
    public ActionResult<ConversationActionResponse> ResetConversation(
        [FromQuery] string? channel = null,
        [FromQuery] string? userId = null,
        [FromQuery] string? conversationId = null)
    {
        var scope = ApiConversationScopeApplier.Apply(_scopeAccessor, channel, userId, conversationId);
        _assistantSessionService.Reset();

        return Ok(new ConversationActionResponse(
            $"Conversation reset for channel={scope.Channel}, userId={scope.UserId}, conversationId={scope.ConversationId}."));
    }

    [HttpPost("messages")]
    [ProducesResponseType(typeof(ConversationMessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ConversationMessageResponse>> PostMessage(
        [FromBody] ConversationMessageRequest request,
        CancellationToken cancellationToken)
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

        var result = await _orchestrator.ProcessAsync(
            request.Input,
            scope.Channel,
            scope.UserId,
            scope.ConversationId,
            cancellationToken);
        return Ok(Map(result));
    }

    private static ConversationMessageResponse Map(ConversationAgentResult result)
    {
        var items = result.Items
            .Select(MapItem)
            .ToList();

        return new ConversationMessageResponse(
            result.AgentName,
            result.Intent,
            result.IsBatch,
            items,
            result.Message);
    }

    private static ConversationSystemPromptHistoryEntryResponse MapPromptHistoryEntry(SystemPromptEntry item)
    {
        return new ConversationSystemPromptHistoryEntryResponse(
            item.Version,
            item.PromptText,
            item.Source,
            item.IsActive,
            item.CreatedAtUtc);
    }

    private static ConversationSystemPromptProposalResponse MapPromptProposal(SystemPromptProposal item)
    {
        return new ConversationSystemPromptProposalResponse(
            item.Id,
            item.ProposedPrompt,
            item.Reason,
            item.Confidence,
            item.Source,
            item.Status,
            item.CreatedAtUtc,
            item.ReviewedAtUtc,
            item.AppliedSystemPromptEntryId);
    }

    private static ConversationMessageItemResponse MapItem(ConversationAgentItemResult item)
    {
        var preview = item.AppendPreview;
        var warning = preview is not null
            ? BuildWarning(preview)
            : null;

        return new ConversationMessageItemResponse(
            item.Input,
            item.FoundInDeck,
            item.AssistantCompletion?.Content,
            item.AssistantCompletion?.Model,
            preview?.Status.ToString(),
            preview?.TargetDeckFileName,
            preview?.TargetDeckPath,
            BuildExistingEntriesPreview(item.Lookup),
            warning,
            preview?.Status == VocabularyAppendPreviewStatus.ReadyToAppend,
            BuildSuggestedPartOfSpeech(item),
            preview?.DuplicateMatches?.Select(MapDeckEntry).ToList());
    }

    private static string? BuildWarning(VocabularyAppendPreviewResult preview)
    {
        return preview.Status switch
        {
            VocabularyAppendPreviewStatus.ParseFailed => preview.Message ?? "Assistant output could not be parsed.",
            VocabularyAppendPreviewStatus.DuplicateFound => preview.Message ?? "Word already exists in writable decks.",
            VocabularyAppendPreviewStatus.NoMatchingDeck => preview.Message ?? "No matching writable deck.",
            VocabularyAppendPreviewStatus.NoWritableDecks => preview.Message ?? "No writable decks were found.",
            _ => null
        };
    }

    private static string? BuildExistingEntriesPreview(VocabularyLookupResult lookup)
    {
        if (!lookup.Found)
        {
            return null;
        }

        var lines = lookup.Matches
            .Take(5)
            .Select(entry => $"{entry.DeckFileName} row {entry.RowNumber}: {entry.Word}");

        return string.Join(" | ", lines);
    }

    private static string? BuildSuggestedPartOfSpeech(ConversationAgentItemResult item)
    {
        if (item.AppendPreview is null)
        {
            return null;
        }

        if (TryExtractPartOfSpeechMarker(item.AssistantCompletion?.Content, out var markerFromReply))
        {
            return markerFromReply;
        }

        if (string.IsNullOrWhiteSpace(item.AppendPreview.TargetDeckFileName))
        {
            return null;
        }

        return VocabularyDeckMarkerSuggester.SuggestMarker(item.AppendPreview.TargetDeckFileName);
    }

    private static bool TryExtractPartOfSpeechMarker(string? assistantReply, out string marker)
    {
        marker = string.Empty;

        if (string.IsNullOrWhiteSpace(assistantReply))
        {
            return false;
        }

        var lines = assistantReply
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var line in lines)
        {
            if (!line.StartsWith("(", StringComparison.Ordinal))
            {
                continue;
            }

            var closeIndex = line.IndexOf(')');
            if (closeIndex <= 1)
            {
                continue;
            }

            var candidate = line[1..closeIndex].Trim();
            if (VocabularyPartOfSpeechCatalog.TryNormalize(candidate, out marker))
            {
                return true;
            }
        }

        return false;
    }

    private static ConversationDeckEntryResponse MapDeckEntry(VocabularyDeckEntry entry)
    {
        return new ConversationDeckEntryResponse(
            entry.DeckFileName,
            entry.DeckPath,
            entry.RowNumber,
            entry.Word,
            entry.Meaning,
            entry.Examples);
    }

}





