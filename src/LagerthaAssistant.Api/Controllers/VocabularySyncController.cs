using Microsoft.AspNetCore.Mvc;
using LagerthaAssistant.Api.Contracts;
using LagerthaAssistant.Application.Interfaces.Vocabulary;

namespace LagerthaAssistant.Api.Controllers;

[ApiController]
[Route("api/vocabulary-sync")]
public sealed class VocabularySyncController : ControllerBase
{
    private readonly IVocabularySyncProcessor _syncProcessor;

    public VocabularySyncController(IVocabularySyncProcessor syncProcessor)
    {
        _syncProcessor = syncProcessor;
    }

    [HttpGet("status")]
    [ProducesResponseType(typeof(VocabularySyncStatusResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<VocabularySyncStatusResponse>> GetStatus(CancellationToken cancellationToken)
    {
        var pending = await _syncProcessor.GetPendingCountAsync(cancellationToken);
        return Ok(new VocabularySyncStatusResponse(pending));
    }

    [HttpPost("run")]
    [ProducesResponseType(typeof(VocabularySyncRunResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<VocabularySyncRunResponse>> Run(
        [FromQuery] int take = 25,
        CancellationToken cancellationToken = default)
    {
        if (take <= 0)
        {
            return BadRequest("Parameter 'take' must be greater than 0.");
        }

        var summary = await _syncProcessor.ProcessPendingAsync(take, cancellationToken);
        return Ok(new VocabularySyncRunResponse(
            summary.Requested,
            summary.Processed,
            summary.Completed,
            summary.Requeued,
            summary.Failed,
            summary.PendingAfterRun));
    }
}
