namespace LagerthaAssistant.Api.Controllers;

using LagerthaAssistant.Api.Contracts;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/miniapp/policy")]
public sealed class MiniAppPolicyController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(MiniAppPolicyResponse), StatusCodes.Status200OK)]
    public ActionResult<MiniAppPolicyResponse> GetPolicy()
    {
        return Ok(MiniAppPolicyPayloadFactory.Create());
    }
}

internal static class MiniAppPolicyPayloadFactory
{
    public static MiniAppPolicyResponse Create()
    {
        return new MiniAppPolicyResponse(
            DefaultLocale: "uk",
            SupportedLocales: ["uk", "en"],
            StorageModePolicy: "graph_only_v1",
            AllowedStorageModes: ["graph"],
            OneDriveAuthScope: "shared_provider_token_v1",
            RequiresInitDataVerification: true,
            Notes:
            [
                "OneDrive token cache is currently shared by provider and not user-scoped.",
                "Mini App v1 must use graph storage mode only.",
                "Client should verify Telegram init data before loading protected data."
            ]);
    }
}
