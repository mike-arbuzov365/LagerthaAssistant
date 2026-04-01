namespace SharedBotKernel.Infrastructure.Telegram;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;

public static class TelegramMiniAppInitDataVerifier
{
    private const string HashKey = "hash";
    private const string AuthDateKey = "auth_date";

    public static TelegramMiniAppInitDataVerificationResult Verify(
        string initData,
        string botToken,
        DateTimeOffset nowUtc,
        TimeSpan maxAge)
    {
        if (string.IsNullOrWhiteSpace(botToken))
        {
            return TelegramMiniAppInitDataVerificationResult.Fail("bot_token_missing");
        }

        if (string.IsNullOrWhiteSpace(initData))
        {
            return TelegramMiniAppInitDataVerificationResult.Fail("init_data_missing");
        }

        var parameters = Parse(initData);
        if (!parameters.TryGetValue(HashKey, out var hash) || string.IsNullOrWhiteSpace(hash))
        {
            return TelegramMiniAppInitDataVerificationResult.Fail("hash_missing");
        }

        if (!parameters.TryGetValue(AuthDateKey, out var authDateValue)
            || !long.TryParse(authDateValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var authDateUnix))
        {
            return TelegramMiniAppInitDataVerificationResult.Fail("auth_date_invalid");
        }

        var authDateUtc = DateTimeOffset.FromUnixTimeSeconds(authDateUnix);
        if (nowUtc - authDateUtc > maxAge)
        {
            return TelegramMiniAppInitDataVerificationResult.Fail("auth_date_expired", authDateUtc);
        }

        if (authDateUtc > nowUtc.AddMinutes(5))
        {
            return TelegramMiniAppInitDataVerificationResult.Fail("auth_date_in_future", authDateUtc);
        }

        var dataCheckString = BuildDataCheckString(parameters);
        var expectedHash = ComputeHashHex(botToken, dataCheckString);
        var isValid = FixedTimeEquals(expectedHash, hash.Trim().ToLowerInvariant());

        return isValid
            ? TelegramMiniAppInitDataVerificationResult.Ok(authDateUtc)
            : TelegramMiniAppInitDataVerificationResult.Fail("hash_mismatch", authDateUtc);
    }

    private static Dictionary<string, string> Parse(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var pairs = query.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var pair in pairs)
        {
            var idx = pair.IndexOf('=');
            if (idx <= 0)
            {
                continue;
            }

            var key = Uri.UnescapeDataString(pair[..idx]);
            var value = Uri.UnescapeDataString(pair[(idx + 1)..]);
            result[key] = value;
        }

        return result;
    }

    private static string BuildDataCheckString(Dictionary<string, string> parameters)
    {
        return string.Join(
            '\n',
            parameters
                .Where(x => !x.Key.Equals(HashKey, StringComparison.Ordinal))
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .Select(x => $"{x.Key}={x.Value}"));
    }

    private static string ComputeHashHex(string botToken, string dataCheckString)
    {
        var secret = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes("WebAppData"),
            Encoding.UTF8.GetBytes(botToken));

        var hash = HMACSHA256.HashData(
            secret,
            Encoding.UTF8.GetBytes(dataCheckString));

        return Convert.ToHexStringLower(hash);
    }

    private static bool FixedTimeEquals(string leftHex, string rightHex)
    {
        try
        {
            var left = Convert.FromHexString(leftHex);
            var right = Convert.FromHexString(rightHex);
            return CryptographicOperations.FixedTimeEquals(left, right);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

public sealed record TelegramMiniAppInitDataVerificationResult(
    bool IsValid,
    string Reason,
    DateTimeOffset? AuthDateUtc)
{
    public static TelegramMiniAppInitDataVerificationResult Ok(DateTimeOffset authDateUtc)
        => new(true, "ok", authDateUtc);

    public static TelegramMiniAppInitDataVerificationResult Fail(string reason, DateTimeOffset? authDateUtc = null)
        => new(false, reason, authDateUtc);
}
