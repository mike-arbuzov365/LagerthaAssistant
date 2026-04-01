namespace LagerthaAssistant.Api.Controllers;

using LagerthaAssistant.Api.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SharedBotKernel.Options;
using SharedBotKernel.Infrastructure.Telegram;

[ApiController]
[Route("api/miniapp/auth")]
public sealed class MiniAppAuthController : ControllerBase
{
    private static readonly TimeSpan MaxInitDataAge = TimeSpan.FromHours(24);

    private readonly TelegramOptions _telegramOptions;

    public MiniAppAuthController(IOptions<TelegramOptions> telegramOptions)
    {
        _telegramOptions = telegramOptions.Value;
    }

    [HttpPost("verify")]
    [ProducesResponseType(typeof(MiniAppAuthVerifyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<MiniAppAuthVerifyResponse> Verify([FromBody] MiniAppAuthVerifyRequest? request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.InitData))
        {
            return BadRequest("initData is required.");
        }

        var result = TelegramMiniAppInitDataVerifier.Verify(
            request.InitData,
            _telegramOptions.BotToken,
            DateTimeOffset.UtcNow,
            MaxInitDataAge);

        return Ok(new MiniAppAuthVerifyResponse(result.IsValid, result.Reason, result.AuthDateUtc));
    }
}
