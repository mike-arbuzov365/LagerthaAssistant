namespace BaguetteDesign.Infrastructure.Notion;

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BaguetteDesign.Application.Interfaces;
using BaguetteDesign.Domain.Entities;
using BaguetteDesign.Infrastructure.Options;
using Microsoft.Extensions.Logging;

public sealed class NotionPortfolioClient : INotionPortfolioClient
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly NotionPortfolioOptions _options;
    private readonly ILogger<NotionPortfolioClient> _logger;

    public NotionPortfolioClient(HttpClient http, NotionPortfolioOptions options, ILogger<NotionPortfolioClient> logger)
    {
        _http = http;
        _options = options;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PortfolioCase>> FetchAllAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured)
        {
            _logger.LogWarning("NotionPortfolioClient is not configured — skipping sync");
            return [];
        }

        var results = new List<PortfolioCase>();
        string? cursor = null;

        do
        {
            var url = $"{_options.ApiBaseUrl}/databases/{_options.DatabaseId}/query";
            var bodyObj = cursor is null
                ? (object)new { page_size = 100 }
                : new { page_size = 100, start_cursor = cursor };

            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(bodyObj), Encoding.UTF8, "application/json")
            };
            AddHeaders(request);

            var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Notion portfolio DB query failed: {Status} — {Error}", (int)response.StatusCode, err);
                throw new HttpRequestException($"Notion portfolio DB query failed: {(int)response.StatusCode}");
            }

            var envelope = await JsonSerializer.DeserializeAsync<NotionQueryEnvelope>(
                await response.Content.ReadAsStreamAsync(cancellationToken), JsonOpts, cancellationToken);

            if (envelope is null) break;

            foreach (var page in envelope.Results)
                results.Add(MapPage(page));

            cursor = envelope.HasMore ? envelope.NextCursor : null;
        }
        while (cursor is not null);

        _logger.LogInformation("Fetched {Count} portfolio cases from Notion", results.Count);
        return results;
    }

    private PortfolioCase MapPage(NotionPageEnvelope page)
    {
        var props = page.Properties;

        return new PortfolioCase
        {
            NotionPageId = page.Id,
            Title = GetTitle(props, _options.TitleProperty),
            Category = GetSelect(props, _options.CategoryProperty) ?? "Other",
            Description = GetRichText(props, _options.DescriptionProperty),
            Tags = GetRichText(props, _options.TagsProperty),
            CoverImageUrl = GetCoverUrl(page.Cover),
            IsActive = !page.Archived
        };
    }

    private static string GetTitle(Dictionary<string, JsonElement> props, string key)
    {
        if (!props.TryGetValue(key, out var el)) return string.Empty;
        if (!el.TryGetProperty("title", out var arr)) return string.Empty;
        return arr.EnumerateArray()
            .Select(x => x.TryGetProperty("plain_text", out var t) ? t.GetString() : null)
            .FirstOrDefault(x => x is not null) ?? string.Empty;
    }

    private static string? GetRichText(Dictionary<string, JsonElement> props, string key)
    {
        if (!props.TryGetValue(key, out var el)) return null;
        if (!el.TryGetProperty("rich_text", out var arr)) return null;
        var joined = string.Concat(arr.EnumerateArray()
            .Select(x => x.TryGetProperty("plain_text", out var t) ? t.GetString() : null)
            .Where(x => x is not null));
        return string.IsNullOrWhiteSpace(joined) ? null : joined;
    }

    private static string? GetSelect(Dictionary<string, JsonElement> props, string key)
    {
        if (!props.TryGetValue(key, out var el)) return null;
        if (!el.TryGetProperty("select", out var sel)) return null;
        if (sel.ValueKind == JsonValueKind.Null) return null;
        return sel.TryGetProperty("name", out var name) ? name.GetString() : null;
    }

    private static string? GetCoverUrl(JsonElement cover)
    {
        if (cover.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) return null;
        if (!cover.TryGetProperty("type", out var type)) return null;

        var typeStr = type.GetString();
        if (typeStr == "external" && cover.TryGetProperty("external", out var ext))
            return ext.TryGetProperty("url", out var url) ? url.GetString() : null;
        if (typeStr == "file" && cover.TryGetProperty("file", out var file))
            return file.TryGetProperty("url", out var fUrl) ? fUrl.GetString() : null;

        return null;
    }

    private void AddHeaders(HttpRequestMessage request)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        request.Headers.Add("Notion-Version", _options.Version);
    }

    private sealed class NotionQueryEnvelope
    {
        [JsonPropertyName("results")] public List<NotionPageEnvelope> Results { get; set; } = [];
        [JsonPropertyName("next_cursor")] public string? NextCursor { get; set; }
        [JsonPropertyName("has_more")] public bool HasMore { get; set; }
    }

    private sealed class NotionPageEnvelope
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("archived")] public bool Archived { get; set; }
        [JsonPropertyName("cover")] public JsonElement Cover { get; set; }
        [JsonPropertyName("properties")] public Dictionary<string, JsonElement> Properties { get; set; } = [];
    }
}
