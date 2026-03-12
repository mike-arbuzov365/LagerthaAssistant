namespace LagerthaAssistant.Infrastructure.Services.Vocabulary;

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Infrastructure.Constants;
using LagerthaAssistant.Infrastructure.Options;
using Microsoft.Extensions.Logging;

public sealed class GraphDriveClient : IGraphDriveClient
{
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(120) };
    private readonly IGraphAuthService _graphAuthService;
    private readonly GraphOptions _graphOptions;
    private readonly ILogger<GraphDriveClient> _logger;

    public GraphDriveClient(
        IGraphAuthService graphAuthService,
        GraphOptions graphOptions,
        ILogger<GraphDriveClient> logger)
    {
        _graphAuthService = graphAuthService;
        _graphOptions = graphOptions;
        _logger = logger;
    }

    public async Task<IReadOnlyList<GraphDriveFile>> ListFilesAsync(CancellationToken cancellationToken = default)
    {
        var accessToken = await _graphAuthService.GetAccessTokenAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException("Graph authentication is required. Run /graph login first.");
        }

        var files = new List<GraphDriveFile>();
        var nextUrl = BuildChildrenUrl();

        while (!string.IsNullOrWhiteSpace(nextUrl))
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, nextUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(BuildGraphErrorMessage("list OneDrive files", response.StatusCode, payload));
            }

            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.TryGetProperty("value", out var value)
                && value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in value.EnumerateArray())
                {
                    if (!item.TryGetProperty("file", out _))
                    {
                        continue;
                    }

                    var id = item.TryGetProperty("id", out var idElement)
                        ? idElement.GetString() ?? string.Empty
                        : string.Empty;
                    var name = item.TryGetProperty("name", out var nameElement)
                        ? nameElement.GetString() ?? string.Empty
                        : string.Empty;
                    var eTag = item.TryGetProperty("eTag", out var tagElement)
                        ? tagElement.GetString() ?? string.Empty
                        : string.Empty;

                    if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    files.Add(new GraphDriveFile(id, name, eTag, BuildDisplayPath(name)));
                }
            }

            if (document.RootElement.TryGetProperty("@odata.nextLink", out var nextLinkElement)
                && nextLinkElement.ValueKind == JsonValueKind.String)
            {
                nextUrl = nextLinkElement.GetString();
            }
            else
            {
                nextUrl = null;
            }
        }

        return files;
    }

    public async Task<byte[]> DownloadFileContentAsync(string itemId, CancellationToken cancellationToken = default)
    {
        var accessToken = await _graphAuthService.GetAccessTokenAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException("Graph authentication is required. Run /graph login first.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{GraphConstants.GraphApiBaseUrl}me/drive/items/{itemId}/content");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(BuildGraphErrorMessage("download OneDrive file", response.StatusCode, payload));
        }

        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    public async Task<GraphUploadResult> UploadFileContentAsync(
        string itemId,
        byte[] content,
        string? expectedETag = null,
        CancellationToken cancellationToken = default)
    {
        var accessToken = await _graphAuthService.GetAccessTokenAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return new GraphUploadResult(false, "Graph authentication is required. Run /graph login first.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Put, $"{GraphConstants.GraphApiBaseUrl}me/drive/items/{itemId}/content")
        {
            Content = new ByteArrayContent(content)
        };

        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        if (!string.IsNullOrWhiteSpace(expectedETag))
        {
            request.Headers.TryAddWithoutValidation("If-Match", expectedETag);
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var updatedETag = ExtractETag(payload);
            return new GraphUploadResult(true, UpdatedETag: updatedETag);
        }

        if (response.StatusCode == HttpStatusCode.PreconditionFailed)
        {
            return new GraphUploadResult(false, "OneDrive version conflict detected. Refresh and try saving again.");
        }

        if ((int)response.StatusCode == 423)
        {
            return new GraphUploadResult(false, "OneDrive file is locked right now. Close it in other apps and retry.");
        }

        _logger.LogWarning("OneDrive upload failed with status {StatusCode}: {Payload}", (int)response.StatusCode, payload);
        return new GraphUploadResult(false, BuildGraphErrorMessage("upload OneDrive file", response.StatusCode, payload));
    }

    private static string? ExtractETag(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.TryGetProperty("eTag", out var eTag)
                && eTag.ValueKind == JsonValueKind.String)
            {
                return eTag.GetString();
            }
        }
        catch
        {
            // Ignore malformed payloads for successful uploads.
        }

        return null;
    }
    private string BuildChildrenUrl()
    {
        var rootPath = NormalizeRootPath(_graphOptions.RootPath);
        return $"{GraphConstants.GraphApiBaseUrl}me/drive/root:{rootPath}:/children?$select=id,name,eTag,file&$top=200";
    }

    private static string NormalizeRootPath(string rawPath)
    {
        var path = rawPath?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/";
        }

        var segments = path
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Uri.EscapeDataString);

        return "/" + string.Join('/', segments);
    }

    private string BuildDisplayPath(string fileName)
    {
        var rootPath = _graphOptions.RootPath?.Trim() ?? "/";
        if (!rootPath.StartsWith("/", StringComparison.Ordinal))
        {
            rootPath = "/" + rootPath;
        }

        return $"{rootPath.TrimEnd('/')}/{fileName}";
    }

    private static string BuildGraphErrorMessage(string operation, HttpStatusCode statusCode, string payload)
    {
        if ((int)statusCode == 401 || (int)statusCode == 403)
        {
            return $"Could not {operation}: authorization failed. Run /graph login.";
        }

        if ((int)statusCode == 404)
        {
            return $"Could not {operation}: OneDrive path was not found.";
        }

        var compactPayload = payload;
        if (compactPayload.Length > 220)
        {
            compactPayload = compactPayload[..220] + "...";
        }

        return $"Could not {operation}: HTTP {(int)statusCode}. {compactPayload}";
    }
}



