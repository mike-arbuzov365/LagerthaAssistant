using LagerthaAssistant.Api.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace LagerthaAssistant.Api.Controllers;

[ApiController]
[Route("api/miniapp/diagnostics")]
public sealed class MiniAppDiagnosticsController : ControllerBase
{
    private readonly ILogger<MiniAppDiagnosticsController> _logger;

    public MiniAppDiagnosticsController(ILogger<MiniAppDiagnosticsController> logger)
    {
        _logger = logger;
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public IActionResult Post([FromBody] MiniAppDiagnosticRequest? request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.SessionId) || string.IsNullOrWhiteSpace(request.EventType))
        {
            return Accepted();
        }

        using (_logger.BeginScope(new Dictionary<string, object?>
        {
            ["MiniAppSessionId"] = request.SessionId,
            ["MiniAppEventType"] = request.EventType,
            ["MiniAppPath"] = request.Path,
            ["MiniAppIsTelegram"] = request.IsTelegram,
            ["MiniAppHostSource"] = request.HostSource,
            ["MiniAppPlatform"] = request.Platform,
            ["MiniAppChannel"] = request.Channel,
            ["MiniAppUserId"] = request.UserId,
            ["MiniAppConversationId"] = request.ConversationId,
            ["MiniAppHasInitData"] = request.HasInitData,
            ["MiniAppHasWebApp"] = request.HasWebApp,
            ["MiniAppLocale"] = request.Locale,
        }))
        {
            var detailText = request.Details is null || request.Details.Count == 0
                ? string.Empty
                : string.Join(", ", request.Details.Select(x => $"{x.Key}={x.Value.ToString()}"));
            var summary = string.IsNullOrWhiteSpace(request.Message)
                ? request.EventType
                : $"{request.EventType}: {request.Message}";

            switch (request.Severity?.Trim().ToLowerInvariant())
            {
                case "error":
                    _logger.LogError("Mini App diagnostic {Summary}. {DetailText}", summary, detailText);
                    break;
                case "warn":
                case "warning":
                    _logger.LogWarning("Mini App diagnostic {Summary}. {DetailText}", summary, detailText);
                    break;
                default:
                    _logger.LogInformation("Mini App diagnostic {Summary}. {DetailText}", summary, detailText);
                    break;
            }
        }

        return Accepted();
    }
}
