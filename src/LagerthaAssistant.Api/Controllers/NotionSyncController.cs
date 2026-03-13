using LagerthaAssistant.Api.Contracts;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using Microsoft.AspNetCore.Mvc;

namespace LagerthaAssistant.Api.Controllers;

[ApiController]
[Route("api/notion-sync")]
public sealed class NotionSyncController : ControllerBase
{
    private readonly INotionSyncProcessor _notionSyncProcessor;

    public NotionSyncController(INotionSyncProcessor notionSyncProcessor)
    {
        _notionSyncProcessor = notionSyncProcessor;
    }

    [HttpGet("status")]
    [ProducesResponseType(typeof(NotionSyncStatusResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<NotionSyncStatusResponse>> GetStatus(CancellationToken cancellationToken = default)
    {
        var status = await _notionSyncProcessor.GetStatusAsync(cancellationToken);
        return Ok(new NotionSyncStatusResponse(
            status.Enabled,
            status.IsConfigured,
            status.Message,
            status.PendingCards,
            status.FailedCards));
    }

    [HttpPost("run")]
    [ProducesResponseType(typeof(NotionSyncRunResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<NotionSyncRunResponse>> Run(
        [FromQuery] int take = 25,
        CancellationToken cancellationToken = default)
    {
        if (take <= 0)
        {
            return BadRequest("Parameter 'take' must be greater than 0.");
        }

        var summary = await _notionSyncProcessor.ProcessPendingAsync(take, cancellationToken);
        return Ok(new NotionSyncRunResponse(
            summary.Requested,
            summary.Processed,
            summary.Completed,
            summary.Requeued,
            summary.Failed,
            summary.PendingAfterRun));
    }

    [HttpGet("failed")]
    [ProducesResponseType(typeof(IReadOnlyList<NotionSyncFailedCardResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<NotionSyncFailedCardResponse>>> GetFailed(
        [FromQuery] int take = 20,
        CancellationToken cancellationToken = default)
    {
        if (take <= 0)
        {
            return BadRequest("Parameter 'take' must be greater than 0.");
        }

        var failed = await _notionSyncProcessor.GetFailedCardsAsync(take, cancellationToken);
        var response = failed
            .Select(item => new NotionSyncFailedCardResponse(
                item.CardId,
                item.Word,
                item.DeckFileName,
                item.StorageMode,
                item.AttemptCount,
                item.LastError,
                item.LastAttemptAtUtc,
                item.LastSeenAtUtc))
            .ToList();

        return Ok(response);
    }

    [HttpPost("retry-failed")]
    [ProducesResponseType(typeof(NotionSyncRetryFailedResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<NotionSyncRetryFailedResponse>> RetryFailed(
        [FromQuery] int take = 25,
        CancellationToken cancellationToken = default)
    {
        if (take <= 0)
        {
            return BadRequest("Parameter 'take' must be greater than 0.");
        }

        var requeued = await _notionSyncProcessor.RequeueFailedAsync(take, cancellationToken);
        var status = await _notionSyncProcessor.GetStatusAsync(cancellationToken);
        return Ok(new NotionSyncRetryFailedResponse(take, requeued, status.PendingCards));
    }
}

