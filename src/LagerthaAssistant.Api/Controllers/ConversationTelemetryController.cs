using Microsoft.AspNetCore.Mvc;
using LagerthaAssistant.Api.Contracts;
using LagerthaAssistant.Application.Interfaces.Agents;

namespace LagerthaAssistant.Api.Controllers;

[ApiController]
[Route("api/telemetry")]
public sealed class ConversationTelemetryController : ControllerBase
{
    private readonly IConversationMetricsService _metricsService;

    public ConversationTelemetryController(IConversationMetricsService metricsService)
    {
        _metricsService = metricsService;
    }

    [HttpGet("intents")]
    [ProducesResponseType(typeof(ConversationIntentMetricsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ConversationIntentMetricsResponse>> GetIntents(
        [FromQuery] int days = 7,
        [FromQuery] int top = 20,
        [FromQuery] string? channel = null,
        CancellationToken cancellationToken = default)
    {
        if (days <= 0 || days > 90)
        {
            return BadRequest("Parameter 'days' must be between 1 and 90.");
        }

        if (top <= 0 || top > 200)
        {
            return BadRequest("Parameter 'top' must be between 1 and 200.");
        }

        var rows = await _metricsService.GetTopIntentsAsync(days, top, channel, cancellationToken);
        var fromDateUtc = DateTime.UtcNow.Date.AddDays(-(days - 1));

        var items = rows
            .Select(row => new ConversationIntentMetricItemResponse(
                row.Channel,
                row.AgentName,
                row.Intent,
                row.IsBatch,
                row.Count,
                row.TotalItems,
                row.LastSeenAtUtc))
            .ToList();

        return Ok(new ConversationIntentMetricsResponse(
            fromDateUtc,
            string.IsNullOrWhiteSpace(channel) ? null : channel.Trim().ToLowerInvariant(),
            days,
            top,
            items));
    }
}
