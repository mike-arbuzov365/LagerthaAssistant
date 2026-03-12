using Microsoft.AspNetCore.Mvc;
using LagerthaAssistant.Api.Contracts;
using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.Interfaces.Agents;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Models.Vocabulary;

namespace LagerthaAssistant.Api.Controllers;

[ApiController]
[Route("api/conversation")]
public sealed class ConversationController : ControllerBase
{
    private const string DefaultChannel = "api";

    private readonly IConversationOrchestrator _orchestrator;

    public ConversationController(IConversationOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }


    [HttpGet("commands")]
    [ProducesResponseType(typeof(IReadOnlyList<ConversationCommandItemResponse>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<ConversationCommandItemResponse>> GetCommands()
    {
        var commands = ConversationCommandCatalog.SlashCommands
            .Select(item => new ConversationCommandItemResponse(item.Category, item.Command, item.Description))
            .ToList();

        return Ok(commands);
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

        var channel = NormalizeChannel(request.Channel);
        var result = await _orchestrator.ProcessAsync(request.Input, channel, cancellationToken);
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

    private static string NormalizeChannel(string? channel)
    {
        var normalized = channel?.Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(normalized)
            ? DefaultChannel
            : normalized;
    }
}
