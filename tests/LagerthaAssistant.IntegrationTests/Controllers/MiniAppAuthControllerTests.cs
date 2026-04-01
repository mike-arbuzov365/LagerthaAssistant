namespace LagerthaAssistant.IntegrationTests.Controllers;

using System.Security.Cryptography;
using System.Text;
using LagerthaAssistant.Api.Contracts;
using LagerthaAssistant.Api.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SharedBotKernel.Options;
using Xunit;

public sealed class MiniAppAuthControllerTests
{
    private const string BotToken = "123456:ABCDEF_fake_token_for_tests";
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-03-31T10:00:00+00:00");

    [Fact]
    public void Verify_ShouldReturnBadRequest_WhenInitDataMissing()
    {
        var sut = CreateSut(BotToken);

        var response = sut.Verify(new MiniAppAuthVerifyRequest("  "));

        Assert.IsType<BadRequestObjectResult>(response.Result);
    }

    [Fact]
    public void Verify_ShouldReturnInvalid_WhenBotTokenMissing()
    {
        var sut = CreateSut(botToken: string.Empty);
        var initData = BuildInitData(
            BotToken,
            authDateUtc: Now.AddMinutes(-1),
            ("query_id", "AAHx"));

        var response = sut.Verify(new MiniAppAuthVerifyRequest(initData));

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<MiniAppAuthVerifyResponse>(ok.Value);

        Assert.False(payload.IsValid);
        Assert.Equal("bot_token_missing", payload.Reason);
    }

    [Fact]
    public void Verify_ShouldReturnOk_WhenSignatureIsValid()
    {
        var sut = CreateSut(BotToken);
        var initData = BuildInitData(
            BotToken,
            authDateUtc: DateTimeOffset.UtcNow.AddMinutes(-1),
            ("query_id", "AAHx"),
            ("user", "{\"id\":123,\"first_name\":\"Mike\"}"));

        var response = sut.Verify(new MiniAppAuthVerifyRequest(initData));

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<MiniAppAuthVerifyResponse>(ok.Value);

        Assert.True(payload.IsValid);
        Assert.Equal("ok", payload.Reason);
        Assert.NotNull(payload.AuthDateUtc);
    }

    private static MiniAppAuthController CreateSut(string botToken)
    {
        var options = Options.Create(new TelegramOptions
        {
            BotToken = botToken
        });

        return new MiniAppAuthController(options);
    }

    private static string BuildInitData(
        string botToken,
        DateTimeOffset authDateUtc,
        params (string Key, string Value)[] fields)
    {
        var parameters = fields.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        parameters["auth_date"] = authDateUtc.ToUnixTimeSeconds().ToString();

        var dataCheckString = string.Join(
            '\n',
            parameters
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .Select(x => $"{x.Key}={x.Value}"));

        var secret = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes("WebAppData"),
            Encoding.UTF8.GetBytes(botToken));
        var hash = HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(dataCheckString));
        parameters["hash"] = Convert.ToHexStringLower(hash);

        return string.Join(
            "&",
            parameters.Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value)}"));
    }
}
