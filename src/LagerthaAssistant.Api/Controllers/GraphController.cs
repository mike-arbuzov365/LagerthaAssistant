using Microsoft.AspNetCore.Mvc;
using LagerthaAssistant.Api.Contracts;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Vocabulary;

namespace LagerthaAssistant.Api.Controllers;

[ApiController]
[Route("api/graph")]
public sealed class GraphController : ControllerBase
{
    private readonly IGraphAuthService _graphAuthService;

    public GraphController(IGraphAuthService graphAuthService)
    {
        _graphAuthService = graphAuthService;
    }

    [HttpGet("status")]
    [ProducesResponseType(typeof(GraphAuthStatusResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<GraphAuthStatusResponse>> GetStatus(CancellationToken cancellationToken = default)
    {
        var status = await _graphAuthService.GetStatusAsync(cancellationToken);
        return Ok(MapStatus(status));
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(GraphLoginResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<GraphLoginResponse>> Login(CancellationToken cancellationToken = default)
    {
        var login = await _graphAuthService.LoginAsync(cancellationToken);
        var status = await _graphAuthService.GetStatusAsync(cancellationToken);

        return Ok(new GraphLoginResponse(login.Succeeded, login.Message, MapStatus(status)));
    }

    [HttpPost("logout")]
    [ProducesResponseType(typeof(GraphAuthStatusResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<GraphAuthStatusResponse>> Logout(CancellationToken cancellationToken = default)
    {
        await _graphAuthService.LogoutAsync(cancellationToken);
        var status = await _graphAuthService.GetStatusAsync(cancellationToken);
        return Ok(MapStatus(status));
    }

    private static GraphAuthStatusResponse MapStatus(GraphAuthStatus status)
    {
        return new GraphAuthStatusResponse(
            status.IsConfigured,
            status.IsAuthenticated,
            status.Message,
            status.AccessTokenExpiresAtUtc);
    }
}
