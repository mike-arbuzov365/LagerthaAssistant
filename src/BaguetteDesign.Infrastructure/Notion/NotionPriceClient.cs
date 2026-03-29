namespace BaguetteDesign.Infrastructure.Notion;

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BaguetteDesign.Application.Interfaces;
using BaguetteDesign.Domain.Entities;
using BaguetteDesign.Infrastructure.Options;
using Microsoft.Extensions.Logging;

public sealed class NotionPriceClient : INotionPriceClient
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly NotionPriceOptions _options;
    private readonly ILogger<NotionPriceClient> _logger;

    public NotionPriceClient(HttpClient http, NotionPriceOptions options, ILogger<NotionPriceClient> logger)
    {
        _http = http;
        _options = options;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PriceItem>> FetchAllAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured)
        {
            _logger.LogWarning("NotionPriceClient is not configured — skipping sync");
            return [];
        }

        var results = new List<PriceItem>();
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
                _logger.LogError("Notion price DB query failed: {Status} — {Error}", (int)response.StatusCode, err);
                throw new HttpRequestException($"Notion price DB query failed: {(int)response.StatusCode}");
            }

            var envelope = await JsonSerializer.DeserializeAsync<NotionQueryEnvelope>(
                await response.Content.ReadAsStreamAsync(cancellationToken), JsonOpts, cancellationToken);

            if (envelope is null) break;

            foreach (var page in envelope.Results)
                results.Add(MapPage(page));

            cursor = envelope.HasMore ? envelope.NextCursor : null;
        }
        while (cursor is not null);

        _logger.LogInformation("Fetched {Count} price items from Notion", results.Count);
        return results;
    }

    private PriceItem MapPage(NotionPageEnvelope page)
    {
        var props = page.Properties;

        return new PriceItem
        {
            NotionPageId = page.Id,
            Name = GetTitle(props, _options.NameProperty),
            Category = GetSelect(props, _options.CategoryProperty) ?? "Other",
            Description = GetRichText(props, _options.DescriptionProperty),
            PriceAmount = GetNumber(props, _options.PriceProperty),
            Currency = GetSelect(props, _options.CurrencyProperty) ?? "USD",
            Country = GetSelect(props, _options.CountryProperty),
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
        var texts = arr.EnumerateArray()
            .Select(x => x.TryGetProperty("plain_text", out var t) ? t.GetString() : null)
            .Where(x => x is not null);
        var joined = string.Join("", texts);
        return string.IsNullOrWhiteSpace(joined) ? null : joined;
    }

    private static string? GetSelect(Dictionary<string, JsonElement> props, string key)
    {
        if (!props.TryGetValue(key, out var el)) return null;
        if (!el.TryGetProperty("select", out var sel)) return null;
        if (sel.ValueKind == JsonValueKind.Null) return null;
        return sel.TryGetProperty("name", out var name) ? name.GetString() : null;
    }

    private static decimal? GetNumber(Dictionary<string, JsonElement> props, string key)
    {
        if (!props.TryGetValue(key, out var el)) return null;
        if (!el.TryGetProperty("number", out var num)) return null;
        if (num.ValueKind == JsonValueKind.Null) return null;
        return num.GetDecimal();
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
        [JsonPropertyName("properties")] public Dictionary<string, JsonElement> Properties { get; set; } = [];
    }
}
