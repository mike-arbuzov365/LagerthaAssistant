namespace LagerthaAssistant.Infrastructure.Services.Food;

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LagerthaAssistant.Application.Interfaces.Food;
using LagerthaAssistant.Application.Models.Food;
using LagerthaAssistant.Infrastructure.Options;
using Microsoft.Extensions.Logging;

public sealed class NotionFoodClient : INotionFoodClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly NotionFoodOptions _options;
    private readonly ILogger<NotionFoodClient> _logger;

    public NotionFoodClient(
        HttpClient httpClient,
        NotionFoodOptions options,
        ILogger<NotionFoodClient> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    public Task<IReadOnlyList<NotionPage>> GetInventoryAsync(CancellationToken cancellationToken = default)
        => QueryAllPagesAsync(_options.InventoryDatabaseId, "Inventory", cancellationToken);

    public Task<IReadOnlyList<NotionPage>> GetMealPlansAsync(CancellationToken cancellationToken = default)
        => QueryAllPagesAsync(_options.MealPlansDatabaseId, "MealPlans", cancellationToken);

    public Task<IReadOnlyList<NotionPage>> GetGroceryListAsync(CancellationToken cancellationToken = default)
        => QueryAllPagesAsync(_options.GroceryListDatabaseId, "GroceryList", cancellationToken);

    public async Task MarkGroceryItemBoughtAsync(string notionPageId, bool bought, CancellationToken cancellationToken = default)
    {
        var url = $"{_options.ApiBaseUrl}/pages/{notionPageId}";
        var body = JsonSerializer.Serialize(new
        {
            properties = new
            {
                Bought = new { checkbox = bought }
            }
        });

        using var request = new HttpRequestMessage(new HttpMethod("PATCH"), url)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        AddHeaders(request);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "Notion PATCH page {PageId} failed: {Status} — {Error}",
                notionPageId,
                (int)response.StatusCode,
                error);
            response.EnsureSuccessStatusCode();
        }
    }

    public async Task<string> CreateGroceryItemAsync(
        string name,
        string? quantity,
        string? store,
        CancellationToken cancellationToken = default)
    {
        var url = $"{_options.ApiBaseUrl}/pages";

        var properties = new Dictionary<string, object>
        {
            ["Item Name"] = new { title = new[] { new { text = new { content = name } } } }
        };

        if (!string.IsNullOrWhiteSpace(quantity))
        {
            properties["Quantity"] = new
            {
                rich_text = new[] { new { text = new { content = quantity } } }
            };
        }

        if (!string.IsNullOrWhiteSpace(store))
        {
            properties["Store"] = new { select = new { name = store } };
        }

        var body = JsonSerializer.Serialize(new
        {
            parent = new { database_id = _options.GroceryListDatabaseId },
            properties
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        AddHeaders(request);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);

        return doc.RootElement.GetProperty("id").GetString()
            ?? throw new InvalidOperationException("Notion create page response did not include an ID.");
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private async Task<IReadOnlyList<NotionPage>> QueryAllPagesAsync(
        string databaseId,
        string label,
        CancellationToken cancellationToken)
    {
        var allPages = new List<NotionPage>();
        string? cursor = null;

        do
        {
            var url = $"{_options.ApiBaseUrl}/databases/{databaseId}/query";
            var bodyObj = cursor is null
                ? (object)new { page_size = 100 }
                : new { page_size = 100, start_cursor = cursor };

            var body = JsonSerializer.Serialize(bodyObj);

            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            AddHeaders(request);

            _logger.LogInformation(
                "Querying Notion database {Label} (id={DatabaseId}) cursor={Cursor}",
                label,
                databaseId,
                cursor ?? "start");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "Notion query {Label} failed: {Status} — {Error}",
                    label,
                    (int)response.StatusCode,
                    error);
                throw new HttpRequestException(
                    $"Notion query {label} failed with status {(int)response.StatusCode}: {error}");
            }

            var envelope = await response.Content.ReadFromJsonAsync<NotionQueryEnvelope>(JsonOptions, cancellationToken);
            if (envelope is null)
            {
                break;
            }

            allPages.AddRange(envelope.Results.Select(MapPage));
            cursor = envelope.HasMore ? envelope.NextCursor : null;

        } while (cursor is not null);

        _logger.LogInformation("Fetched total {Count} pages from Notion database {Label}", allPages.Count, label);
        return allPages;
    }

    private static NotionPage MapPage(NotionPageEnvelope page)
    {
        var properties = new Dictionary<string, NotionPropertyValue>();

        foreach (var (key, raw) in page.Properties)
        {
            properties[key] = MapProperty(raw);
        }

        return new NotionPage(page.Id, page.LastEditedTime, properties, ParsePageIconEmoji(page.Icon));
    }

    private static string? ParsePageIconEmoji(JsonElement icon)
    {
        if (icon.ValueKind == JsonValueKind.Undefined || icon.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (!icon.TryGetProperty("type", out var typeProp))
        {
            return null;
        }

        if (!string.Equals(typeProp.GetString(), "emoji", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!icon.TryGetProperty("emoji", out var emojiProp))
        {
            return null;
        }

        var emoji = emojiProp.GetString();
        return string.IsNullOrWhiteSpace(emoji) ? null : emoji.Trim();
    }

    private static NotionPropertyValue MapProperty(JsonElement raw)
    {
        var type = raw.TryGetProperty("type", out var typeProp)
            ? typeProp.GetString() ?? string.Empty
            : string.Empty;

        IReadOnlyList<NotionRichTextItem>? title = null;
        IReadOnlyList<NotionRichTextItem>? richText = null;
        NotionSelectValue? select = null;
        decimal? number = null;
        bool? checkbox = null;
        NotionDateValue? date = null;
        IReadOnlyList<NotionRelationItem>? relation = null;

        switch (type)
        {
            case "title" when raw.TryGetProperty("title", out var arr):
                title = MapRichTextArray(arr);
                break;

            case "rich_text" when raw.TryGetProperty("rich_text", out var arr):
                richText = MapRichTextArray(arr);
                break;

            case "select" when raw.TryGetProperty("select", out var sel) && sel.ValueKind != JsonValueKind.Null:
                select = new NotionSelectValue(sel.GetProperty("name").GetString() ?? string.Empty);
                break;

            case "number" when raw.TryGetProperty("number", out var num) && num.ValueKind != JsonValueKind.Null:
                number = num.GetDecimal();
                break;

            case "checkbox" when raw.TryGetProperty("checkbox", out var cb):
                checkbox = cb.GetBoolean();
                break;

            case "date" when raw.TryGetProperty("date", out var d) && d.ValueKind != JsonValueKind.Null:
                date = new NotionDateValue(d.GetProperty("start").GetString() ?? string.Empty);
                break;

            case "relation" when raw.TryGetProperty("relation", out var rel):
                var items = new List<NotionRelationItem>();
                foreach (var item in rel.EnumerateArray())
                {
                    if (item.TryGetProperty("id", out var relId))
                    {
                        items.Add(new NotionRelationItem(relId.GetString() ?? string.Empty));
                    }
                }
                relation = items;
                break;
        }

        return new NotionPropertyValue(type, title, richText, select, number, checkbox, date, relation);
    }

    private static List<NotionRichTextItem> MapRichTextArray(JsonElement arr)
    {
        var items = new List<NotionRichTextItem>();
        foreach (var item in arr.EnumerateArray())
        {
            var plain = item.TryGetProperty("plain_text", out var pt)
                ? pt.GetString() ?? string.Empty
                : string.Empty;
            items.Add(new NotionRichTextItem(plain));
        }
        return items;
    }

    private void AddHeaders(HttpRequestMessage request)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        request.Headers.Add("Notion-Version", _options.Version);
    }

    // ── Local Notion envelope types ──────────────────────────────────────────

    private sealed class NotionQueryEnvelope
    {
        public List<NotionPageEnvelope> Results { get; set; } = [];
        public string? NextCursor { get; set; }
        public bool HasMore { get; set; }
    }

    private sealed class NotionPageEnvelope
    {
        public string Id { get; set; } = string.Empty;
        public string LastEditedTime { get; set; } = string.Empty;
        public JsonElement Icon { get; set; }
        public Dictionary<string, JsonElement> Properties { get; set; } = [];
    }
}
