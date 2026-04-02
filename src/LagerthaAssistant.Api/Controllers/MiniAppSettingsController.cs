using LagerthaAssistant.Api.Contracts;
using LagerthaAssistant.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace LagerthaAssistant.Api.Controllers;

[ApiController]
[Route("api/miniapp/settings")]
public sealed class MiniAppSettingsController : ControllerBase
{
    private readonly IConversationScopeAccessor _scopeAccessor;
    private readonly MiniAppSettingsCommitService _commitService;

    public MiniAppSettingsController(
        IConversationScopeAccessor scopeAccessor,
        MiniAppSettingsCommitService commitService)
    {
        _scopeAccessor = scopeAccessor;
        _commitService = commitService;
    }

    [HttpPost("commit")]
    [ProducesResponseType(typeof(MiniAppSettingsCommitResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MiniAppSettingsCommitResponse>> Commit(
        [FromBody] MiniAppSettingsCommitRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return BadRequest("Request body is required.");
        }

        var scope = ApiConversationScopeApplier.Apply(
            _scopeAccessor,
            request.Channel,
            request.UserId,
            request.ConversationId);

        var result = await _commitService.CommitAsync(scope, request, cancellationToken);
        if (!result.Succeeded)
        {
            return BadRequest(result.ErrorMessage);
        }

        return Ok(result.Response);
    }
}
