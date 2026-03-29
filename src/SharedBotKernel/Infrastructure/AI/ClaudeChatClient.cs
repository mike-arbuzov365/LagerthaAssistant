namespace SharedBotKernel.Infrastructure.AI;

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SharedBotKernel.Domain.AI;
using SharedBotKernel.Constants;
using SharedBotKernel.Models.AI;
using SharedBotKernel.Options;
using Microsoft.Extensions.Logging;

public sealed class ClaudeChatClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ClaudeOptions _options;
    private readonly ILogger<ClaudeChatClient> _logger;

    public ClaudeChatClient(ClaudeOptions options, ILogger<ClaudeChatClient> logger)
    {
        _options = options;
        _logger = logger;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_options.BaseUrl),
            Timeout = TimeSpan.FromSeconds(ClaudeConstants.HttpTimeoutSeconds)
        };
    }

    public async Task<AssistantCompletionResult> CompleteAsync(
        IReadOnlyCollection<ConversationMessage> messages,
        string model,
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        var systemPrompt = string.Join(
            Environment.NewLine + Environment.NewLine,
            messages
                .Where(m => m.Role == MessageRole.System)
                .Select(m => m.Content?.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))!);

        var apiMessages = messages
            .Where(m => m.Role == MessageRole.User || m.Role == MessageRole.Assistant)
            .Select(m => new
            {
                role = m.Role == MessageRole.User ? "user" : "assistant",
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = m.Content
                    }
                }
            })
            .ToArray();

        var requestBody = new
        {
            model,
            max_tokens = _options.MaxTokens,
            temperature = _options.Temperature,
            system = string.IsNullOrWhiteSpace(systemPrompt) ? null : systemPrompt,
            messages = apiMessages
        };

        var payload = JsonSerializer.Serialize(requestBody, JsonOptions);
        using var request = new HttpRequestMessage(HttpMethod.Post, ClaudeConstants.MessagesEndpoint)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add(ClaudeConstants.AnthropicVersionHeader, ClaudeConstants.AnthropicVersion);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var rawResponse = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Claude API request failed with status {StatusCode}: {Response}",
                (int)response.StatusCode,
                rawResponse);
            throw new InvalidOperationException($"Claude API request failed with status {(int)response.StatusCode}.");
        }

        using var jsonDocument = JsonDocument.Parse(rawResponse);
        var root = jsonDocument.RootElement;
        var responseModel = root.TryGetProperty("model", out var modelJson)
            ? modelJson.GetString() ?? model
            : model;

        var assistantContent = ExtractAssistantContent(root);
        if (string.IsNullOrWhiteSpace(assistantContent))
        {
            throw new InvalidOperationException("Claude returned an empty response.");
        }

        var usage = ExtractUsage(root);
        return new AssistantCompletionResult(assistantContent.Trim(), responseModel, usage);
    }

    private static AssistantUsage? ExtractUsage(JsonElement root)
    {
        if (!root.TryGetProperty("usage", out var usageJson))
        {
            return null;
        }

        var prompt = usageJson.TryGetProperty("input_tokens", out var promptJson) ? promptJson.GetInt32() : 0;
        var completion = usageJson.TryGetProperty("output_tokens", out var completionJson) ? completionJson.GetInt32() : 0;
        var total = prompt + completion;

        return new AssistantUsage(prompt, completion, total);
    }

    private static string ExtractAssistantContent(JsonElement root)
    {
        if (!root.TryGetProperty("content", out var content)
            || content.ValueKind != JsonValueKind.Array
            || content.GetArrayLength() == 0)
        {
            return string.Empty;
        }

        return string.Join(
            string.Empty,
            content.EnumerateArray()
                .Where(x => x.TryGetProperty("type", out var type) && type.GetString() == "text")
                .Select(x => x.TryGetProperty("text", out var text) ? text.GetString() : string.Empty)
                .Where(x => !string.IsNullOrWhiteSpace(x)));
    }
}
