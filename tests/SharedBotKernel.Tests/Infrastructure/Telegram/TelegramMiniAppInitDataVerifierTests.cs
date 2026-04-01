namespace SharedBotKernel.Tests.Infrastructure.Telegram;

using System.Security.Cryptography;
using System.Text;
using SharedBotKernel.Infrastructure.Telegram;
using Xunit;

public sealed class TelegramMiniAppInitDataVerifierTests
{
    private const string BotToken = "123456:ABCDEF_fake_token_for_tests";
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-03-31T10:00:00+00:00");

    [Fact]
    public void Verify_ReturnsOk_ForSignedPayload()
    {
        var initData = BuildInitData(
            BotToken,
            authDateUtc: Now.AddMinutes(-1),
            ("query_id", "AAHx"),
            ("user", "{\"id\":123,\"first_name\":\"Mike\"}"));

        var result = TelegramMiniAppInitDataVerifier.Verify(initData, BotToken, Now, TimeSpan.FromHours(24));

        Assert.True(result.IsValid);
        Assert.Equal("ok", result.Reason);
    }

    [Fact]
    public void Verify_Fails_ForExpiredAuthDate()
    {
        var initData = BuildInitData(
            BotToken,
            authDateUtc: Now.AddDays(-2),
            ("query_id", "AAHx"));

        var result = TelegramMiniAppInitDataVerifier.Verify(initData, BotToken, Now, TimeSpan.FromHours(24));

        Assert.False(result.IsValid);
        Assert.Equal("auth_date_expired", result.Reason);
    }

    [Fact]
    public void Verify_Fails_ForHashMismatch()
    {
        var initData = BuildInitData(
            BotToken,
            authDateUtc: Now.AddMinutes(-1),
            ("query_id", "AAHx")) + "0";

        var result = TelegramMiniAppInitDataVerifier.Verify(initData, BotToken, Now, TimeSpan.FromHours(24));

        Assert.False(result.IsValid);
        Assert.Equal("hash_mismatch", result.Reason);
    }

    [Fact]
    public void Verify_Fails_ForMissingBotToken()
    {
        var initData = BuildInitData(
            BotToken,
            authDateUtc: Now.AddMinutes(-1),
            ("query_id", "AAHx"));

        var result = TelegramMiniAppInitDataVerifier.Verify(initData, "   ", Now, TimeSpan.FromHours(24));

        Assert.False(result.IsValid);
        Assert.Equal("bot_token_missing", result.Reason);
    }

    [Fact]
    public void Verify_Fails_ForMissingHash()
    {
        var initData = "auth_date=1711860000&query_id=AAHx";

        var result = TelegramMiniAppInitDataVerifier.Verify(initData, BotToken, Now, TimeSpan.FromHours(24));

        Assert.False(result.IsValid);
        Assert.Equal("hash_missing", result.Reason);
    }

    [Fact]
    public void Verify_Fails_ForAuthDateInFuture()
    {
        var initData = BuildInitData(
            BotToken,
            authDateUtc: Now.AddMinutes(6),
            ("query_id", "AAHx"));

        var result = TelegramMiniAppInitDataVerifier.Verify(initData, BotToken, Now, TimeSpan.FromHours(24));

        Assert.False(result.IsValid);
        Assert.Equal("auth_date_in_future", result.Reason);
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
