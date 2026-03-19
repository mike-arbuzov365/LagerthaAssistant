namespace LagerthaAssistant.Infrastructure.Services.Vocabulary;

using System.Text.Json;
using System.Text.Json.Serialization;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.Infrastructure.Constants;
using LagerthaAssistant.Infrastructure.Options;
using Microsoft.Extensions.Logging;

public sealed class GraphAuthService : IGraphAuthService
{
    private const string DefaultVerificationUri = "https://www.microsoft.com/link";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly GraphOptions _options;
    private readonly HttpClient _httpClient;
    private readonly ILogger<GraphAuthService> _logger;
    private readonly SemaphoreSlim _sync = new(1, 1);

    public GraphAuthService(
        GraphOptions options,
        ILogger<GraphAuthService> logger,
        HttpClient? httpClient = null)
    {
        _options = options;
        _logger = logger;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
    }

    public async Task<GraphAuthStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured())
        {
            return new GraphAuthStatus(false, false, "Graph integration is not configured. Set Graph.ClientId first.");
        }

        var accessToken = await GetAccessTokenAsync(cancellationToken);
        var cache = await LoadTokenCacheAsync(cancellationToken);
        if (cache is null)
        {
            return new GraphAuthStatus(true, false, "Not authenticated. Use /graph login.");
        }

        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            cache = await LoadTokenCacheAsync(cancellationToken) ?? cache;
            return new GraphAuthStatus(true, true, "Authenticated.", cache.AccessTokenExpiresAtUtc);
        }

        if (cache.AccessTokenExpiresAtUtc <= DateTimeOffset.UtcNow.AddMinutes(1))
        {
            return new GraphAuthStatus(true, false, "Access token expired. Use /graph login.", cache.AccessTokenExpiresAtUtc);
        }

        return string.IsNullOrWhiteSpace(cache.AccessToken)
            ? new GraphAuthStatus(true, false, "Not authenticated. Use /graph login.")
            : new GraphAuthStatus(true, true, "Authenticated.", cache.AccessTokenExpiresAtUtc);
    }

    public Task<GraphLoginResult> LoginAsync(CancellationToken cancellationToken = default)
    {
        return LoginCoreAsync(null, cancellationToken);
    }

    public Task<GraphLoginResult> LoginAsync(
        Func<GraphDeviceCodePrompt, CancellationToken, Task> onDeviceCodeReceived,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onDeviceCodeReceived);
        return LoginCoreAsync(onDeviceCodeReceived, cancellationToken);
    }

    private async Task<GraphLoginResult> LoginCoreAsync(
        Func<GraphDeviceCodePrompt, CancellationToken, Task>? onDeviceCodeReceived,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured())
        {
            return new GraphLoginResult(false, "Graph login is not available because Graph.ClientId is not configured.");
        }

        await _sync.WaitAsync(cancellationToken);
        try
        {
            var deviceCode = await RequestDeviceCodeAsync(cancellationToken);
            if (deviceCode is null)
            {
                return new GraphLoginResult(false, "Failed to start device-code login flow.");
            }

            var challenge = BuildDeviceLoginChallenge(deviceCode);

            if (onDeviceCodeReceived is not null)
            {
                var prompt = BuildDeviceCodePrompt(challenge);
                await onDeviceCodeReceived(prompt, cancellationToken);
            }

            return await CompleteLoginCoreAsync(challenge, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Graph login failed due to an unexpected error.");
            return new GraphLoginResult(false, $"Graph login failed: {ex.Message}");
        }
        finally
        {
            _sync.Release();
        }
    }

    public async Task<GraphDeviceLoginStartResult> StartLoginAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured())
        {
            return new GraphDeviceLoginStartResult(
                false,
                "Graph login is not available because Graph.ClientId is not configured.");
        }

        await _sync.WaitAsync(cancellationToken);
        try
        {
            var deviceCode = await RequestDeviceCodeAsync(cancellationToken);
            if (deviceCode is null)
            {
                return new GraphDeviceLoginStartResult(false, "Failed to start device-code login flow.");
            }

            var challenge = BuildDeviceLoginChallenge(deviceCode);
            return new GraphDeviceLoginStartResult(
                true,
                "Device code generated. Open verification URI and then call complete login.",
                challenge);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Graph device-code login flow.");
            return new GraphDeviceLoginStartResult(false, $"Graph login failed: {ex.Message}");
        }
        finally
        {
            _sync.Release();
        }
    }

    public async Task<GraphLoginResult> CompleteLoginAsync(
        GraphDeviceLoginChallenge challenge,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured())
        {
            return new GraphLoginResult(false, "Graph login is not available because Graph.ClientId is not configured.");
        }

        if (string.IsNullOrWhiteSpace(challenge.DeviceCode))
        {
            return new GraphLoginResult(false, "Graph login failed: device code is required.");
        }

        await _sync.WaitAsync(cancellationToken);
        try
        {
            return await CompleteLoginCoreAsync(challenge, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Graph complete-login failed due to an unexpected error.");
            return new GraphLoginResult(false, $"Graph login failed: {ex.Message}");
        }
        finally
        {
            _sync.Release();
        }
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        await _sync.WaitAsync(cancellationToken);
        try
        {
            var cachePath = ResolveTokenCachePath();
            if (File.Exists(cachePath))
            {
                File.Delete(cachePath);
            }
        }
        finally
        {
            _sync.Release();
        }
    }

    public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured())
        {
            return null;
        }

        await _sync.WaitAsync(cancellationToken);
        try
        {
            var cache = await LoadTokenCacheAsync(cancellationToken);
            if (cache is null)
            {
                return null;
            }

            if (cache.AccessTokenExpiresAtUtc > DateTimeOffset.UtcNow.AddMinutes(2)
                && !string.IsNullOrWhiteSpace(cache.AccessToken))
            {
                return cache.AccessToken;
            }

            if (string.IsNullOrWhiteSpace(cache.RefreshToken))
            {
                return null;
            }

            var refreshed = await RequestTokenByRefreshTokenAsync(cache.RefreshToken, cancellationToken);
            if (refreshed is null || string.IsNullOrWhiteSpace(refreshed.AccessToken))
            {
                return null;
            }

            await SaveTokenCacheAsync(refreshed, cancellationToken);
            return refreshed.AccessToken;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh Graph access token.");
            return null;
        }
        finally
        {
            _sync.Release();
        }
    }

    private bool IsConfigured()
    {
        return !string.IsNullOrWhiteSpace(_options.ClientId);
    }

    private async Task<DeviceCodeResponse?> RequestDeviceCodeAsync(CancellationToken cancellationToken)
    {
        var endpoint = BuildLoginEndpoint("devicecode");
        var request = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["scope"] = BuildScopeString()
        });

        using var response = await _httpClient.PostAsync(endpoint, request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Graph device code request failed with status {StatusCode}: {Payload}", (int)response.StatusCode, payload);
            return null;
        }

        var model = JsonSerializer.Deserialize<DeviceCodeResponse>(payload, JsonOptions);
        if (model is null)
        {
            return null;
        }

        model.VerificationUri = NormalizeVerificationUri(model.VerificationUri);
        return model;
    }

    private static GraphDeviceCodePrompt BuildDeviceCodePrompt(GraphDeviceLoginChallenge challenge)
    {
        return new GraphDeviceCodePrompt(
            challenge.UserCode.Trim(),
            NormalizeVerificationUri(challenge.VerificationUri),
            string.IsNullOrWhiteSpace(challenge.Message)
                ? null
                : challenge.Message.Trim());
    }

    private static GraphDeviceLoginChallenge BuildDeviceLoginChallenge(DeviceCodeResponse deviceCode)
    {
        var intervalSeconds = Math.Max(3, deviceCode.Interval);
        var expiresInSeconds = Math.Max(15, deviceCode.ExpiresIn);

        return new GraphDeviceLoginChallenge(
            deviceCode.DeviceCode.Trim(),
            deviceCode.UserCode.Trim(),
            NormalizeVerificationUri(deviceCode.VerificationUri),
            expiresInSeconds,
            intervalSeconds,
            DateTimeOffset.UtcNow.AddSeconds(expiresInSeconds),
            string.IsNullOrWhiteSpace(deviceCode.Message)
                ? null
                : deviceCode.Message.Trim());
    }

    private async Task<GraphLoginResult> CompleteLoginCoreAsync(
        GraphDeviceLoginChallenge challenge,
        CancellationToken cancellationToken)
    {
        var intervalSeconds = Math.Max(3, challenge.IntervalSeconds);
        var expiresAt = challenge.ExpiresAtUtc > DateTimeOffset.UtcNow
            ? challenge.ExpiresAtUtc
            : DateTimeOffset.UtcNow.AddSeconds(Math.Max(15, challenge.ExpiresInSeconds));

        while (DateTimeOffset.UtcNow < expiresAt)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), cancellationToken);

            var tokenResponse = await RequestTokenByDeviceCodeAsync(challenge.DeviceCode, cancellationToken);
            if (tokenResponse is null)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
            {
                await SaveTokenCacheAsync(tokenResponse, cancellationToken);
                return new GraphLoginResult(true, "Graph login completed successfully.");
            }

            var errorCode = tokenResponse.Error?.Trim();
            if (string.Equals(errorCode, "authorization_pending", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(errorCode, "slow_down", StringComparison.OrdinalIgnoreCase))
            {
                intervalSeconds += 2;
                continue;
            }

            if (string.Equals(errorCode, "authorization_declined", StringComparison.OrdinalIgnoreCase))
            {
                return new GraphLoginResult(false, "Graph login was declined by the user.");
            }

            if (string.Equals(errorCode, "expired_token", StringComparison.OrdinalIgnoreCase))
            {
                return new GraphLoginResult(false, "Graph login code expired. Run /graph login again.");
            }

            var errorMessage = string.IsNullOrWhiteSpace(tokenResponse.ErrorDescription)
                ? "Graph login failed."
                : $"Graph login failed: {tokenResponse.ErrorDescription}";

            return new GraphLoginResult(false, errorMessage);
        }

        return new GraphLoginResult(false, "Graph login timed out. Run /graph login again.");
    }

    private static string NormalizeVerificationUri(string? verificationUri)
    {
        if (string.IsNullOrWhiteSpace(verificationUri))
        {
            return DefaultVerificationUri;
        }

        var normalized = verificationUri.Trim();
        if (normalized.Contains("microsoftonline.com", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("microsoft.com/devicelogin", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("login.microsoft.com/device", StringComparison.OrdinalIgnoreCase))
        {
            return DefaultVerificationUri;
        }

        return normalized;
    }

    private async Task<TokenResponse?> RequestTokenByDeviceCodeAsync(string deviceCode, CancellationToken cancellationToken)
    {
        var endpoint = BuildLoginEndpoint("token");
        var request = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
            ["client_id"] = _options.ClientId,
            ["device_code"] = deviceCode
        });

        using var response = await _httpClient.PostAsync(endpoint, request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<TokenResponse>(payload, JsonOptions);
    }

    private async Task<TokenResponse?> RequestTokenByRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var endpoint = BuildLoginEndpoint("token");
        var request = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = _options.ClientId,
            ["refresh_token"] = refreshToken,
            ["scope"] = BuildScopeString()
        });

        using var response = await _httpClient.PostAsync(endpoint, request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Graph refresh token request failed with status {StatusCode}: {Payload}", (int)response.StatusCode, payload);
            return null;
        }

        return JsonSerializer.Deserialize<TokenResponse>(payload, JsonOptions);
    }

    private async Task<TokenCacheEntry?> LoadTokenCacheAsync(CancellationToken cancellationToken)
    {
        var cachePath = ResolveTokenCachePath();
        if (!File.Exists(cachePath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(cachePath, cancellationToken);
        return JsonSerializer.Deserialize<TokenCacheEntry>(json, JsonOptions);
    }

    private async Task SaveTokenCacheAsync(TokenResponse tokenResponse, CancellationToken cancellationToken)
    {
        var cachePath = ResolveTokenCachePath();
        var directory = Path.GetDirectoryName(cachePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var entry = new TokenCacheEntry
        {
            AccessToken = tokenResponse.AccessToken,
            RefreshToken = tokenResponse.RefreshToken,
            AccessTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(Math.Max(30, tokenResponse.ExpiresIn))
        };

        var json = JsonSerializer.Serialize(entry, JsonOptions);
        await File.WriteAllTextAsync(cachePath, json, cancellationToken);
    }

    private string ResolveTokenCachePath()
    {
        return Environment.ExpandEnvironmentVariables(_options.TokenCachePath);
    }

    private string BuildScopeString()
    {
        var scopes = _options.Scopes?
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .Select(scope => scope.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];

        if (!scopes.Contains("offline_access", StringComparer.OrdinalIgnoreCase))
        {
            scopes.Add("offline_access");
        }

        return string.Join(' ', scopes);
    }

    private string BuildLoginEndpoint(string action)
    {
        var tenant = string.IsNullOrWhiteSpace(_options.TenantId)
            ? "common"
            : _options.TenantId.Trim();

        return $"{GraphConstants.LoginBaseUrl}{tenant}/oauth2/v2.0/{action}";
    }

    private sealed class DeviceCodeResponse
    {
        [JsonPropertyName("device_code")]
        public string DeviceCode { get; set; } = string.Empty;
        [JsonPropertyName("user_code")]
        public string UserCode { get; set; } = string.Empty;
        [JsonPropertyName("verification_uri")]
        public string VerificationUri { get; set; } = string.Empty;
        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
        [JsonPropertyName("interval")]
        public int Interval { get; set; }
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;
        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; } = string.Empty;
        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
        [JsonPropertyName("error")]
        public string Error { get; set; } = string.Empty;
        [JsonPropertyName("error_description")]
        public string ErrorDescription { get; set; } = string.Empty;
    }

    private sealed class TokenCacheEntry
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTimeOffset AccessTokenExpiresAtUtc { get; set; }
    }
}
