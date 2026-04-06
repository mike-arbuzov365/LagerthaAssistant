namespace SharedBotKernel.Infrastructure.AI;

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SharedBotKernel.Domain.AI;
using SharedBotKernel.Constants;
using SharedBotKernel.Models.AI;
using SharedBotKernel.Options;
using Microsoft.Extensions.Logging;

public sealed class GeminiChatClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiChatClient> _logger;

    public GeminiChatClient(GeminiOptions options, ILogger<GeminiChatClient> logger)
        : this(options, logger, httpClient: null)
    {
    }

    public GeminiChatClient(GeminiOptions options, ILogger<GeminiChatClient> logger, HttpClient? httpClient)
    {
        _options = options;
        _logger = logger;

        _httpClient = httpClient ?? new HttpClient(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2)
        });

        _httpClient.BaseAddress ??= new Uri(_options.BaseUrl);
        if (httpClient is null)
        {
            _httpClient.Timeout = TimeSpan.FromSeconds(GeminiConstants.HttpTimeoutSeconds);
        }
    }

    public async Task<AssistantCompletionResult> CompleteAsync(
        IReadOnlyCollection<ConversationMessage> messages,
        string model,
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        var systemParts = messages
            .Where(m => m.Role == MessageRole.System)
            .Select(m => new { text = m.Content?.Trim() })
            .Where(p => !string.IsNullOrWhiteSpace(p.text))
            .ToArray();

        var contents = messages
            .Where(m => m.Role == MessageRole.User || m.Role == MessageRole.Assistant)
            .Select(m => new
            {
                role = m.Role == MessageRole.User ? "user" : "model",
                parts = new[] { new { text = m.Content } }
            })
            .ToArray();

        var requestBody = new
        {
            systemInstruction = systemParts.Length > 0
                ? new { parts = systemParts }
                : null,
            contents,
            generationConfig = new
            {
                temperature = _options.Temperature,
                maxOutputTokens = _options.MaxTokens
            }
        };

        var payload = JsonSerializer.Serialize(requestBody, JsonOptions);
        var endpoint = string.Format(GeminiConstants.GenerateContentEndpoint, model) + $"?key={apiKey}";
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var rawResponse = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Gemini API request failed with status {StatusCode}: {Response}",
                (int)response.StatusCode,
                rawResponse);
            throw new InvalidOperationException($"Gemini API request failed with status {(int)response.StatusCode}.");
        }

        using var jsonDocument = JsonDocument.Parse(rawResponse);
        var root = jsonDocument.RootElement;

        var responseModel = root.TryGetProperty("modelVersion", out var modelJson)
            ? modelJson.GetString() ?? model
            : model;

        var assistantContent = ExtractAssistantContent(root);
        if (string.IsNullOrWhiteSpace(assistantContent))
        {
            throw new InvalidOperationException("Gemini returned an empty response.");
        }

        var usage = ExtractUsage(root);
        return new AssistantCompletionResult(assistantContent.Trim(), responseModel, usage);
    }

    private static AssistantUsage? ExtractUsage(JsonElement root)
    {
        if (!root.TryGetProperty("usageMetadata", out var usageJson))
        {
            return null;
        }

        var prompt = usageJson.TryGetProperty("promptTokenCount", out var promptJson) ? promptJson.GetInt32() : 0;
        var completion = usageJson.TryGetProperty("candidatesTokenCount", out var completionJson) ? completionJson.GetInt32() : 0;
        var total = usageJson.TryGetProperty("totalTokenCount", out var totalJson) ? totalJson.GetInt32() : prompt + completion;

        return new AssistantUsage(prompt, completion, total);
    }

    private static string ExtractAssistantContent(JsonElement root)
    {
        if (!root.TryGetProperty("candidates", out var candidates)
            || candidates.ValueKind != JsonValueKind.Array
            || candidates.GetArrayLength() == 0)
        {
            return string.Empty;
        }

        var first = candidates[0];
        if (!first.TryGetProperty("content", out var content)
            || !content.TryGetProperty("parts", out var parts)
            || parts.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        return string.Join(
            string.Empty,
            parts.EnumerateArray()
                .Select(p => p.TryGetProperty("text", out var text) ? text.GetString() : string.Empty)
                .Where(t => !string.IsNullOrWhiteSpace(t)));
    }
}
