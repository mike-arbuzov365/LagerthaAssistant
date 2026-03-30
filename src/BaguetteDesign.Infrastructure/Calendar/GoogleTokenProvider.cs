namespace BaguetteDesign.Infrastructure.Calendar;

using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BaguetteDesign.Application.Interfaces;
using BaguetteDesign.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

/// <summary>
/// Obtains a short-lived OAuth2 access token using a Google Service Account JSON key.
/// Tokens are cached in memory until 60 seconds before expiry.
/// </summary>
public sealed class GoogleTokenProvider : IGoogleTokenProvider
{
    private const string TokenUrl = "https://oauth2.googleapis.com/token";
    private const string CalendarScope = "https://www.googleapis.com/auth/calendar";

    private readonly HttpClient _http;
    private readonly GoogleCalendarOptions _options;
    private readonly ILogger<GoogleTokenProvider> _logger;

    private string? _cachedToken;
    private DateTimeOffset _tokenExpiry = DateTimeOffset.MinValue;

    public GoogleTokenProvider(HttpClient http, GoogleCalendarOptions options, ILogger<GoogleTokenProvider> logger)
    {
        _http = http;
        _options = options;
        _logger = logger;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedToken is not null && DateTimeOffset.UtcNow < _tokenExpiry)
            return _cachedToken;

        var sa = JsonSerializer.Deserialize<ServiceAccountKey>(_options.ServiceAccountJson)
            ?? throw new InvalidOperationException("Failed to deserialize Google service account JSON.");

        var jwt = BuildJwt(sa);
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
            ["assertion"] = jwt
        });

        var response = await _http.PostAsync(TokenUrl, form, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Google token exchange failed: {Status} — {Body}", (int)response.StatusCode, body);
            throw new InvalidOperationException($"Google token exchange failed: {(int)response.StatusCode}");
        }

        var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(body)
            ?? throw new InvalidOperationException("Failed to parse Google token response.");

        _cachedToken = tokenResponse.AccessToken;
        _tokenExpiry = DateTimeOffset.UtcNow.AddSeconds(tokenResponse.ExpiresIn - 60);

        return _cachedToken;
    }

    private static string BuildJwt(ServiceAccountKey sa)
    {
        var now = DateTimeOffset.UtcNow;
        var rsa = RSA.Create();
        rsa.ImportFromPem(sa.PrivateKey);

        var key = new RsaSecurityKey(rsa);
        var credentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Iss, sa.ClientEmail),
            new Claim(JwtRegisteredClaimNames.Sub, sa.ClientEmail),
            new Claim(JwtRegisteredClaimNames.Aud, "https://oauth2.googleapis.com/token"),
            new Claim("scope", CalendarScope),
            new Claim(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new Claim(JwtRegisteredClaimNames.Exp, now.AddMinutes(60).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        var token = new JwtSecurityToken(claims: claims, signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class ServiceAccountKey
    {
        [JsonPropertyName("client_email")] public string ClientEmail { get; set; } = string.Empty;
        [JsonPropertyName("private_key")] public string PrivateKey { get; set; } = string.Empty;
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")] public string AccessToken { get; set; } = string.Empty;
        [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
    }
}
