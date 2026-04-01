namespace LagerthaAssistant.IntegrationTests.Controllers;

using LagerthaAssistant.Api.Contracts;
using LagerthaAssistant.Api.Controllers;
using Microsoft.AspNetCore.Mvc;
using Xunit;

public sealed class MiniAppPolicyControllerTests
{
    [Fact]
    public void GetPolicy_ShouldReturnExpectedV1Constraints()
    {
        var sut = new MiniAppPolicyController();

        var response = sut.GetPolicy();

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<MiniAppPolicyResponse>(ok.Value);

        Assert.Equal("uk", payload.DefaultLocale);
        Assert.Equal(["uk", "en"], payload.SupportedLocales);
        Assert.Equal("graph_only_v1", payload.StorageModePolicy);
        Assert.Equal(["graph"], payload.AllowedStorageModes);
        Assert.Equal("shared_provider_token_v1", payload.OneDriveAuthScope);
        Assert.True(payload.RequiresInitDataVerification);
        Assert.NotEmpty(payload.Notes);
    }
}
