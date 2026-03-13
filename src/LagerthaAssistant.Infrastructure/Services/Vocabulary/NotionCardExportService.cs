namespace LagerthaAssistant.Infrastructure.Services.Vocabulary;

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.Infrastructure.Options;
using Microsoft.Extensions.Logging;

public sealed class NotionCardExportService : INotionCardExportService, IDisposable
{
    private const int MaxRichTextChunkLength = 1800;
    private const int MaxRichTextChunks = 20;
    private const string DefaultApiBaseUrl = "https://api.notion.com/v1";
    private const string DefaultApiVersion = "2022-06-28";

    private readonly NotionOptions _options;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly ILogger<NotionCardExportService> _logger;

    public NotionCardExportService(NotionOptions options, ILogger<NotionCardExportService> logger)
    {
        _options = options;
        _logger = logger;
        _httpClient = CreateDefaultClient(options);
        _ownsHttpClient = true;
    }

    public NotionCardExportService(NotionOptions options, HttpClient httpClient, ILogger<NotionCardExportService> logger)
    {
        _options = options;
        _logger = logger;
        _httpClient = httpClient;
        _ownsHttpClient = false;

        if (_httpClient.BaseAddress is null)
        {
            _httpClient.BaseAddress = new Uri(BuildApiBaseUrl(_options.ApiBaseUrl));
        }
    }

    public NotionExportStatus GetStatus()
    {
        if (!_options.Enabled)
        {
            return new NotionExportStatus(false, false, "Notion integration is disabled.");
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey) || string.IsNullOrWhiteSpace(_options.DatabaseId))
        {
            return new NotionExportStatus(
                Enabled: true,
                IsConfigured: false,
                Message: "Notion is not configured. Set Notion:ApiKey and Notion:DatabaseId.");
        }

        return new NotionExportStatus(true, true, "Notion integration is configured.");
    }

    public async Task<NotionCardExportResult> ExportAsync(
        NotionCardExportRequest request,
        CancellationToken cancellationToken = default)
    {
        var status = GetStatus();
        if (!status.Enabled || !status.IsConfigured)
        {
            return new NotionCardExportResult(
                NotionCardExportOutcome.Failed,
                IsRecoverableFailure: false,
                ErrorMessage: status.Message);
        }

        if (!IsRequestValid(request, out var validationError))
        {
            return new NotionCardExportResult(
                NotionCardExportOutcome.Failed,
                IsRecoverableFailure: false,
                ErrorMessage: validationError);
        }

        try
        {
            var conflictMode = ParseConflictMode(_options.ConflictMode);
            var key = request.IdentityKey.Trim();

            var pageId = string.IsNullOrWhiteSpace(request.ExistingPageId)
                ? await FindPageIdByKeyAsync(key, cancellationToken)
                : request.ExistingPageId.Trim();

            if (!string.IsNullOrWhiteSpace(pageId))
            {
                if (conflictMode == NotionConflictMode.Skip)
                {
                    return new NotionCardExportResult(
                        NotionCardExportOutcome.Skipped,
                        IsRecoverableFailure: false,
                        PageId: pageId);
                }

                if (conflictMode == NotionConflictMode.Error)
                {
                    return new NotionCardExportResult(
                        NotionCardExportOutcome.Failed,
                        IsRecoverableFailure: false,
                        ErrorMessage: "Notion conflict detected and conflict rule set to 'error'.",
                        PageId: pageId);
                }

                return await UpdatePageAsync(pageId, request, cancellationToken);
            }

            return await CreatePageAsync(request, cancellationToken);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Notion export timed out for card #{CardId}", request.CardId);
            return new NotionCardExportResult(
                NotionCardExportOutcome.Failed,
                IsRecoverableFailure: true,
                ErrorMessage: "Notion request timed out.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Notion export HTTP failure for card #{CardId}", request.CardId);
            return new NotionCardExportResult(
                NotionCardExportOutcome.Failed,
                IsRecoverableFailure: true,
                ErrorMessage: ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Notion export failed unexpectedly for card #{CardId}", request.CardId);
            return new NotionCardExportResult(
                NotionCardExportOutcome.Failed,
                IsRecoverableFailure: false,
                ErrorMessage: ex.Message);
        }
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private async Task<NotionCardExportResult> CreatePageAsync(
        NotionCardExportRequest request,
        CancellationToken cancellationToken)
    {
        var payload = new
        {
            parent = new { database_id = _options.DatabaseId.Trim() },
            properties = BuildProperties(request)
        };

        var response = await SendJsonAsync(HttpMethod.Post, "pages", payload, cancellationToken);
        if (!response.Succeeded)
        {
            return new NotionCardExportResult(
                NotionCardExportOutcome.Failed,
                response.IsRecoverable,
                response.ErrorMessage);
        }

        return new NotionCardExportResult(
            NotionCardExportOutcome.Created,
            IsRecoverableFailure: false,
            PageId: response.PageId);
    }

    private async Task<NotionCardExportResult> UpdatePageAsync(
        string pageId,
        NotionCardExportRequest request,
        CancellationToken cancellationToken)
    {
        var payload = new
        {
            properties = BuildProperties(request)
        };

        var response = await SendJsonAsync(new HttpMethod("PATCH"), $"pages/{pageId}", payload, cancellationToken);
        if (!response.Succeeded)
        {
            return new NotionCardExportResult(
                NotionCardExportOutcome.Failed,
                response.IsRecoverable,
                response.ErrorMessage,
                pageId);
        }

        return new NotionCardExportResult(
            NotionCardExportOutcome.Updated,
            IsRecoverableFailure: false,
            PageId: response.PageId ?? pageId);
    }

    private async Task<string?> FindPageIdByKeyAsync(string key, CancellationToken cancellationToken)
    {
        var payload = new
        {
            filter = new
            {
                property = _options.KeyPropertyName,
                rich_text = new
                {
                    equals = key
                }
            },
            page_size = 1
        };

        var response = await SendJsonAsync(HttpMethod.Post, $"databases/{_options.DatabaseId.Trim()}/query", payload, cancellationToken);
        if (!response.Succeeded)
        {
            if (!response.IsRecoverable)
            {
                throw new InvalidOperationException(response.ErrorMessage);
            }

            throw new HttpRequestException(response.ErrorMessage);
        }

        return response.PageId;
    }

    private async Task<NotionHttpResponse> SendJsonAsync(
        HttpMethod method,
        string relativePath,
        object payload,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload);
        using var request = new HttpRequestMessage(method, relativePath)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey.Trim());
        request.Headers.TryAddWithoutValidation("Notion-Version", NormalizeVersion(_options.Version));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return new NotionHttpResponse(true, false, null, ExtractPageId(content));
        }

        var message = BuildErrorMessage(response.StatusCode, content);
        return new NotionHttpResponse(false, IsRecoverableStatusCode(response.StatusCode), message, null);
    }

    private Dictionary<string, object?> BuildProperties(NotionCardExportRequest request)
    {
        var properties = new Dictionary<string, object?>(StringComparer.Ordinal);

        AddProperty(properties, _options.WordPropertyName, new
        {
            title = BuildRichTextArray(string.IsNullOrWhiteSpace(request.Word) ? "-" : request.Word.Trim())
        });

        AddProperty(properties, _options.KeyPropertyName, new
        {
            rich_text = BuildRichTextArray(request.IdentityKey.Trim())
        });

        AddProperty(properties, _options.MeaningPropertyName, new
        {
            rich_text = BuildRichTextArray(request.Meaning)
        });

        AddProperty(properties, _options.ExamplesPropertyName, new
        {
            rich_text = BuildRichTextArray(request.Examples)
        });

        if (!string.IsNullOrWhiteSpace(request.PartOfSpeechMarker))
        {
            AddProperty(properties, _options.PartOfSpeechPropertyName, new
            {
                rich_text = BuildRichTextArray(request.PartOfSpeechMarker)
            });
        }

        AddProperty(properties, _options.DeckPropertyName, new
        {
            rich_text = BuildRichTextArray(request.DeckFileName)
        });

        AddProperty(properties, _options.StorageModePropertyName, new
        {
            rich_text = BuildRichTextArray(request.StorageMode)
        });

        AddProperty(properties, _options.RowNumberPropertyName, new
        {
            number = request.RowNumber
        });

        AddProperty(properties, _options.LastSeenPropertyName, new
        {
            date = new
            {
                start = request.LastSeenAtUtc.UtcDateTime.ToString("O")
            }
        });

        return properties;
    }

    private static object[] BuildRichTextArray(string? value)
    {
        var safeValue = string.IsNullOrWhiteSpace(value)
            ? "-"
            : value.Trim();

        var chunks = ChunkRichText(safeValue)
            .Take(MaxRichTextChunks)
            .Select(chunk => new
            {
                type = "text",
                text = new
                {
                    content = chunk
                }
            })
            .Cast<object>()
            .ToArray();

        if (chunks.Length == 0)
        {
            return
            [
                new
                {
                    type = "text",
                    text = new
                    {
                        content = "-"
                    }
                }
            ];
        }

        return chunks;
    }

    private static IEnumerable<string> ChunkRichText(string value)
    {
        if (value.Length <= MaxRichTextChunkLength)
        {
            yield return value;
            yield break;
        }

        for (var index = 0; index < value.Length; index += MaxRichTextChunkLength)
        {
            var size = Math.Min(MaxRichTextChunkLength, value.Length - index);
            yield return value.Substring(index, size);
        }
    }

    private static bool IsRequestValid(NotionCardExportRequest request, out string? error)
    {
        if (request.CardId <= 0)
        {
            error = "Invalid card identifier for Notion export.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.IdentityKey))
        {
            error = "Notion identity key is empty.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.Word))
        {
            error = "Word is empty for Notion export.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool IsRecoverableStatusCode(HttpStatusCode statusCode)
    {
        var code = (int)statusCode;
        return code == 408
            || code == 409
            || code == 425
            || code == 429
            || code >= 500;
    }

    private static string BuildErrorMessage(HttpStatusCode statusCode, string payload)
    {
        var code = (int)statusCode;
        var message = ExtractNotionMessage(payload);
        if (string.IsNullOrWhiteSpace(message))
        {
            return $"Notion request failed with HTTP {code}.";
        }

        return $"Notion request failed with HTTP {code}: {message}";
    }

    private static string? ExtractNotionMessage(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.TryGetProperty("message", out var messageElement)
                && messageElement.ValueKind == JsonValueKind.String)
            {
                return messageElement.GetString();
            }
        }
        catch
        {
            // Ignore parsing failures and use fallback.
        }

        var compact = payload.Trim();
        if (compact.Length > 220)
        {
            compact = compact[..220] + "...";
        }

        return compact;
    }

    private static string? ExtractPageId(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(payload);

            if (document.RootElement.TryGetProperty("results", out var results)
                && results.ValueKind == JsonValueKind.Array
                && results.GetArrayLength() > 0)
            {
                var first = results[0];
                if (first.TryGetProperty("id", out var firstId) && firstId.ValueKind == JsonValueKind.String)
                {
                    return firstId.GetString();
                }
            }

            if (document.RootElement.TryGetProperty("id", out var idElement)
                && idElement.ValueKind == JsonValueKind.String)
            {
                return idElement.GetString();
            }
        }
        catch
        {
            // Ignore malformed payloads.
        }

        return null;
    }

    private static NotionConflictMode ParseConflictMode(string? raw)
    {
        if (string.Equals(raw, "skip", StringComparison.OrdinalIgnoreCase))
        {
            return NotionConflictMode.Skip;
        }

        if (string.Equals(raw, "error", StringComparison.OrdinalIgnoreCase))
        {
            return NotionConflictMode.Error;
        }

        return NotionConflictMode.Update;
    }

    private static string BuildApiBaseUrl(string? raw)
    {
        var value = string.IsNullOrWhiteSpace(raw)
            ? DefaultApiBaseUrl
            : raw.Trim();

        return value.TrimEnd('/') + "/";
    }

    private static string NormalizeVersion(string? version)
    {
        return string.IsNullOrWhiteSpace(version)
            ? DefaultApiVersion
            : version.Trim();
    }

    private static HttpClient CreateDefaultClient(NotionOptions options)
    {
        return new HttpClient
        {
            BaseAddress = new Uri(BuildApiBaseUrl(options.ApiBaseUrl)),
            Timeout = TimeSpan.FromSeconds(Math.Clamp(options.RequestTimeoutSeconds, 5, 300))
        };
    }

    private static void AddProperty(IDictionary<string, object?> properties, string propertyName, object? value)
    {
        if (string.IsNullOrWhiteSpace(propertyName) || value is null)
        {
            return;
        }

        properties[propertyName.Trim()] = value;
    }

    private readonly record struct NotionHttpResponse(
        bool Succeeded,
        bool IsRecoverable,
        string? ErrorMessage,
        string? PageId);

    private enum NotionConflictMode
    {
        Update = 1,
        Skip = 2,
        Error = 3
    }
}
