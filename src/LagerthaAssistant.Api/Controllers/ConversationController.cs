using Microsoft.AspNetCore.Mvc;
using LagerthaAssistant.Api.Contracts;
using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.Interfaces.AI;
using LagerthaAssistant.Application.Interfaces.Agents;
using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Models.Vocabulary;

namespace LagerthaAssistant.Api.Controllers;

[ApiController]
[Route("api/conversation")]
public sealed class ConversationController : ControllerBase
{
    private const string DefaultChannel = "api";

    private readonly IConversationOrchestrator _orchestrator;
    private readonly IAssistantSessionService _assistantSessionService;
    private readonly IConversationScopeAccessor _scopeAccessor;

    public ConversationController(
        IConversationOrchestrator orchestrator,
        IAssistantSessionService assistantSessionService,
        IConversationScopeAccessor scopeAccessor)
    {
        _orchestrator = orchestrator;
        _assistantSessionService = assistantSessionService;
        _scopeAccessor = scopeAccessor;
    }

    [HttpGet("commands")]
    [ProducesResponseType(typeof(IReadOnlyList<ConversationCommandItemResponse>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<ConversationCommandItemResponse>> GetCommands()
    {
        return Ok(BuildCommandItems());
    }

    [HttpGet("commands/grouped")]
    [ProducesResponseType(typeof(IReadOnlyList<ConversationCommandGroupResponse>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<ConversationCommandGroupResponse>> GetGroupedCommands()
    {
        return Ok(BuildCommandGroups());
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
        var scope = BuildScope(channel, userId, conversationId);
        _scopeAccessor.Set(scope);

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
        var scope = BuildScope(channel, userId, conversationId);
        _scopeAccessor.Set(scope);

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

        var channelName = NormalizeChannel(request.Channel);
        var result = await _orchestrator.ProcessAsync(
            request.Input,
            channelName,
            request.UserId,
            request.ConversationId,
            cancellationToken);
        return Ok(Map(result));
    }

    private static IReadOnlyList<ConversationCommandItemResponse> BuildCommandItems()
    {
        return ConversationCommandCatalog.SlashCommands
            .Select(item => new ConversationCommandItemResponse(item.Category, item.Command, item.Description))
            .ToList();
    }

    private static IReadOnlyList<ConversationCommandGroupResponse> BuildCommandGroups()
    {
        return ConversationCommandCatalog.SlashCommandGroups
            .Select(group =>
                new ConversationCommandGroupResponse(
                    group.Category,
                    group.Commands
                        .Select(item => new ConversationCommandItemResponse(item.Category, item.Command, item.Description))
                        .ToList()))
            .ToList();
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
            warning);
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

    private static ConversationScope BuildScope(string? channel, string? userId, string? conversationId)
    {
        return ConversationScope.Create(
            NormalizeChannel(channel),
            userId,
            conversationId);
    }

    private static string NormalizeChannel(string? channel)
    {
        var normalized = channel?.Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(normalized)
            ? DefaultChannel
            : normalized;
    }
}

