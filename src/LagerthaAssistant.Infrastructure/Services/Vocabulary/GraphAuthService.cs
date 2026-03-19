namespace LagerthaAssistant.Infrastructure.Services.Vocabulary;

using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.Infrastructure.Constants;
using LagerthaAssistant.Infrastructure.Data;
using LagerthaAssistant.Infrastructure.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public sealed class GraphAuthService : IGraphAuthService
{
    private const string DefaultVerificationUri = "https://www.microsoft.com/link";
    private const string OneDriveProviderKey = "onedrive";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly Regex UnixStyleEnvironmentVariableRegex = new(
        @"\$(\{(?<name>[A-Za-z_][A-Za-z0-9_]*)\}|(?<name>[A-Za-z_][A-Za-z0-9_]*))",
        RegexOptions.Compiled);

    private static readonly Regex WindowsStyleUnresolvedEnvironmentVariableRegex = new(
        @"%[A-Za-z_][A-Za-z0-9_]*%",
        RegexOptions.Compiled);

    private readonly GraphOptions _options;
    private readonly HttpClient _httpClient;
    private readonly ILogger<GraphAuthService> _logger;
    private readonly IServiceScopeFactory? _serviceScopeFactory;
    private readonly SemaphoreSlim _sync = new(1, 1);
    private int _tokenCachePathLogged;

    public GraphAuthService(
        GraphOptions options,
        ILogger<GraphAuthService> logger,
        HttpClient? httpClient = null,
        IServiceScopeFactory? serviceScopeFactory = null)
    {
        _options = options;
        _logger = logger;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        _serviceScopeFactory = serviceScopeFactory;
    }

    public async Task<GraphAuthStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured())
        {
            _logger.LogWarning("Graph status check: integration is not configured (Graph.ClientId is empty).");
            return new GraphAuthStatus(false, false, "Graph integration is not configured. Set Graph.ClientId first.");
        }

        var accessToken = await GetAccessTokenAsync(cancellationToken);
        var cache = await LoadTokenCacheAsync(cancellationToken);
        if (cache is null)
        {
            _logger.LogWarning("Graph status check: not authenticated (token cache not found in database or file).");
            return new GraphAuthStatus(true, false, "Not authenticated. Use /graph login.");
        }

        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            cache = await LoadTokenCacheAsync(cancellationToken) ?? cache;
            _logger.LogInformation(
                "Graph status check: authenticated. Access token expires at {ExpiresAtUtc}.",
                cache.AccessTokenExpiresAtUtc);
            return new GraphAuthStatus(true, true, "Authenticated.", cache.AccessTokenExpiresAtUtc);
        }

        if (cache.AccessTokenExpiresAtUtc <= DateTimeOffset.UtcNow.AddMinutes(1))
        {
            _logger.LogWarning(
                "Graph status check: token is expired at {ExpiresAtUtc} and refresh did not produce a usable access token.",
                cache.AccessTokenExpiresAtUtc);
            return new GraphAuthStatus(true, false, "Access token expired. Use /graph login.", cache.AccessTokenExpiresAtUtc);
        }

        if (string.IsNullOrWhiteSpace(cache.AccessToken))
        {
            _logger.LogWarning(
                "Graph status check: access token is empty while cache record exists. ExpiresAtUtc={ExpiresAtUtc}, HasRefreshToken={HasRefreshToken}.",
                cache.AccessTokenExpiresAtUtc,
                !string.IsNullOrWhiteSpace(cache.RefreshToken));
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
            await DeleteTokenCacheFromDatabaseAsync(cancellationToken);

            var cachePath = ResolveTokenCachePath();
            if (File.Exists(cachePath))
            {
                File.Delete(cachePath);
                _logger.LogInformation("Graph logout: token cache file removed at {TokenCachePath}.", cachePath);
            }
            else
            {
                _logger.LogInformation("Graph logout: token cache file not found at {TokenCachePath}.", cachePath);
            }

            _logger.LogInformation("Graph logout completed.");
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
                _logger.LogInformation("Graph access token request: token cache is empty.");
                return null;
            }

            if (cache.AccessTokenExpiresAtUtc > DateTimeOffset.UtcNow.AddMinutes(2)
                && !string.IsNullOrWhiteSpace(cache.AccessToken))
            {
                _logger.LogInformation(
                    "Graph access token request: using cached access token. ExpiresAtUtc={ExpiresAtUtc}.",
                    cache.AccessTokenExpiresAtUtc);
                return cache.AccessToken;
            }

            if (string.IsNullOrWhiteSpace(cache.RefreshToken))
            {
                _logger.LogWarning(
                    "Graph access token request: refresh token is missing. ExpiresAtUtc={ExpiresAtUtc}.",
                    cache.AccessTokenExpiresAtUtc);
                return null;
            }

            var refreshed = await RequestTokenByRefreshTokenAsync(cache.RefreshToken, cancellationToken);
            if (refreshed is null || string.IsNullOrWhiteSpace(refreshed.AccessToken))
            {
                _logger.LogWarning(
                    "Graph access token request: refresh token flow did not return a valid access token. ExpiresAtUtc={ExpiresAtUtc}.",
                    cache.AccessTokenExpiresAtUtc);
                return null;
            }

            await SaveTokenCacheAsync(refreshed, cancellationToken);
            _logger.LogInformation("Graph access token request: access token refreshed successfully.");
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
        var dbEntry = await LoadTokenCacheFromDatabaseAsync(cancellationToken);
        if (dbEntry is not null)
        {
            _logger.LogInformation(
                "Graph token cache source: database. ExpiresAtUtc={ExpiresAtUtc}, HasRefreshToken={HasRefreshToken}.",
                dbEntry.AccessTokenExpiresAtUtc,
                !string.IsNullOrWhiteSpace(dbEntry.RefreshToken));
            await TryMirrorTokenCacheToFileAsync(dbEntry, cancellationToken);
            return dbEntry;
        }

        var fileEntry = await LoadTokenCacheFromFileAsync(cancellationToken);
        if (fileEntry is not null)
        {
            _logger.LogInformation(
                "Graph token cache source: file. ExpiresAtUtc={ExpiresAtUtc}, HasRefreshToken={HasRefreshToken}.",
                fileEntry.AccessTokenExpiresAtUtc,
                !string.IsNullOrWhiteSpace(fileEntry.RefreshToken));
            await TrySaveTokenCacheToDatabaseAsync(fileEntry, cancellationToken);
        }
        else
        {
            _logger.LogInformation("Graph token cache source: none (database and file are empty).");
        }

        return fileEntry;
    }

    private async Task SaveTokenCacheAsync(TokenResponse tokenResponse, CancellationToken cancellationToken)
    {
        var current = await LoadTokenCacheAsync(cancellationToken);
        var refreshToken = string.IsNullOrWhiteSpace(tokenResponse.RefreshToken)
            ? current?.RefreshToken ?? string.Empty
            : tokenResponse.RefreshToken;

        var entry = new TokenCacheEntry
        {
            AccessToken = tokenResponse.AccessToken,
            RefreshToken = refreshToken,
            AccessTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(Math.Max(30, tokenResponse.ExpiresIn))
        };

        await TrySaveTokenCacheToDatabaseAsync(entry, cancellationToken);
        await TryMirrorTokenCacheToFileAsync(entry, cancellationToken);
        _logger.LogInformation(
            "Graph token cache updated. ExpiresAtUtc={ExpiresAtUtc}, HasRefreshToken={HasRefreshToken}.",
            entry.AccessTokenExpiresAtUtc,
            !string.IsNullOrWhiteSpace(entry.RefreshToken));
    }

    private string ResolveTokenCachePath()
    {
        var configuredPath = _options.TokenCachePath?.Trim();
        var expandedPath = string.IsNullOrWhiteSpace(configuredPath)
            ? GetDefaultTokenCachePath()
            : ExpandEnvironmentVariablesPortable(configuredPath);

        var normalizedPath = NormalizePathSeparators(expandedPath);
        if (HasUnresolvedEnvironmentVariables(normalizedPath))
        {
            normalizedPath = GetDefaultTokenCachePath();
        }

        if (!Path.IsPathRooted(normalizedPath))
        {
            normalizedPath = Path.GetFullPath(normalizedPath, AppContext.BaseDirectory);
        }

        if (Interlocked.Exchange(ref _tokenCachePathLogged, 1) == 0)
        {
            _logger.LogInformation("Graph token cache path resolved to: {TokenCachePath}", normalizedPath);
        }

        return normalizedPath;
    }

    private async Task<TokenCacheEntry?> LoadTokenCacheFromDatabaseAsync(CancellationToken cancellationToken)
    {
        if (_serviceScopeFactory is null)
        {
            return null;
        }

        try
        {
            await using var scope = _serviceScopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var token = await db.GraphAuthTokens
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Provider == OneDriveProviderKey, cancellationToken);

            if (token is null)
            {
                _logger.LogInformation("Graph token cache was not found in database for provider {Provider}.", OneDriveProviderKey);
                return null;
            }

            _logger.LogInformation(
                "Graph token cache loaded from database for provider {Provider}. ExpiresAtUtc={ExpiresAtUtc}, UpdatedAtUtc={UpdatedAtUtc}.",
                OneDriveProviderKey,
                token.AccessTokenExpiresAtUtc,
                token.UpdatedAtUtc);

            return new TokenCacheEntry
            {
                AccessToken = token.AccessToken,
                RefreshToken = token.RefreshToken,
                AccessTokenExpiresAtUtc = token.AccessTokenExpiresAtUtc
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load Graph token cache from database.");
            return null;
        }
    }

    private async Task DeleteTokenCacheFromDatabaseAsync(CancellationToken cancellationToken)
    {
        if (_serviceScopeFactory is null)
        {
            return;
        }

        try
        {
            await using var scope = _serviceScopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var token = await db.GraphAuthTokens
                .SingleOrDefaultAsync(x => x.Provider == OneDriveProviderKey, cancellationToken);

            if (token is null)
            {
                _logger.LogInformation("Graph token cache delete: nothing to delete for provider {Provider}.", OneDriveProviderKey);
                return;
            }

            db.GraphAuthTokens.Remove(token);
            await db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Graph token cache deleted from database for provider {Provider}.", OneDriveProviderKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete Graph token cache from database.");
        }
    }

    private async Task<TokenCacheEntry?> LoadTokenCacheFromFileAsync(CancellationToken cancellationToken)
    {
        try
        {
            var cachePath = ResolveTokenCachePath();
            if (!File.Exists(cachePath))
            {
                _logger.LogInformation("Graph token cache file was not found at {TokenCachePath}.", cachePath);
                return null;
            }

            var json = await File.ReadAllTextAsync(cachePath, cancellationToken);
            var entry = JsonSerializer.Deserialize<TokenCacheEntry>(json, JsonOptions);
            if (entry is null)
            {
                _logger.LogWarning("Graph token cache file exists but could not be deserialized: {TokenCachePath}.", cachePath);
                return null;
            }

            _logger.LogInformation("Graph token cache loaded from file: {TokenCachePath}.", cachePath);
            return entry;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read Graph token cache from file.");
            return null;
        }
    }

    private async Task TryMirrorTokenCacheToFileAsync(TokenCacheEntry entry, CancellationToken cancellationToken)
    {
        try
        {
            var cachePath = ResolveTokenCachePath();
            var directory = Path.GetDirectoryName(cachePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(entry, JsonOptions);
            await File.WriteAllTextAsync(cachePath, json, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not mirror Graph token cache to file.");
        }
    }

    private async Task TrySaveTokenCacheToDatabaseAsync(TokenCacheEntry entry, CancellationToken cancellationToken)
    {
        if (_serviceScopeFactory is null)
        {
            return;
        }

        try
        {
            await using var scope = _serviceScopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var token = await db.GraphAuthTokens
                .SingleOrDefaultAsync(x => x.Provider == OneDriveProviderKey, cancellationToken);

            if (token is null)
            {
                db.GraphAuthTokens.Add(new Domain.Entities.GraphAuthToken
                {
                    Provider = OneDriveProviderKey,
                    AccessToken = entry.AccessToken,
                    RefreshToken = entry.RefreshToken,
                    AccessTokenExpiresAtUtc = entry.AccessTokenExpiresAtUtc,
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                });
            }
            else
            {
                token.AccessToken = entry.AccessToken;
                token.RefreshToken = entry.RefreshToken;
                token.AccessTokenExpiresAtUtc = entry.AccessTokenExpiresAtUtc;
                token.UpdatedAtUtc = DateTimeOffset.UtcNow;
            }

            await db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Graph token cache persisted to database for provider {Provider}. ExpiresAtUtc={ExpiresAtUtc}.",
                OneDriveProviderKey,
                entry.AccessTokenExpiresAtUtc);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist Graph token cache to database.");
        }
    }

    private static bool HasUnresolvedEnvironmentVariables(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (WindowsStyleUnresolvedEnvironmentVariableRegex.IsMatch(path))
        {
            return true;
        }

        return UnixStyleEnvironmentVariableRegex.IsMatch(path);
    }

    private static string NormalizePathSeparators(string path)
    {
        return path
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);
    }

    private static string ExpandEnvironmentVariablesPortable(string rawPath)
    {
        var expanded = Environment.ExpandEnvironmentVariables(rawPath);
        return UnixStyleEnvironmentVariableRegex.Replace(
            expanded,
            match =>
            {
                var variableName = match.Groups["name"].Value;
                if (string.IsNullOrWhiteSpace(variableName))
                {
                    return match.Value;
                }

                var value = Environment.GetEnvironmentVariable(variableName);
                return string.IsNullOrWhiteSpace(value)
                    ? match.Value
                    : value;
            });
    }

    private static string GetDefaultTokenCachePath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            if (!string.IsNullOrWhiteSpace(xdgDataHome))
            {
                localAppData = xdgDataHome;
            }
            else
            {
                var home = Environment.GetEnvironmentVariable("HOME");
                localAppData = !string.IsNullOrWhiteSpace(home)
                    ? Path.Combine(home, ".local", "share")
                    : Path.GetTempPath();
            }
        }

        return Path.Combine(localAppData, "LagerthaAssistant", "graph-token.json");
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
