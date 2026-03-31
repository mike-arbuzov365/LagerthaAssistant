namespace SharedBotKernel.Infrastructure.AI;

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SharedBotKernel.Domain.AI;
using SharedBotKernel.Constants;
using SharedBotKernel.Models.AI;
using SharedBotKernel.Options;
using Microsoft.Extensions.Logging;

public sealed class OpenAiChatClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly OpenAiOptions _options;
    private readonly ILogger<OpenAiChatClient> _logger;

    public OpenAiChatClient(OpenAiOptions options, ILogger<OpenAiChatClient> logger)
    {
        _options = options;
        _logger = logger;
        _httpClient = new HttpClient(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2)
        })
        {
            BaseAddress = new Uri(_options.BaseUrl),
            Timeout = TimeSpan.FromSeconds(OpenAiConstants.HttpTimeoutSeconds)
        };
    }

    public async Task<AssistantCompletionResult> CompleteAsync(
        IReadOnlyCollection<ConversationMessage> messages,
        string model,
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        var requestBody = new
        {
            model,
            temperature = _options.Temperature,
            messages = messages.Select(m => new
            {
                role = ToApiRole(m.Role),
                content = m.Content
            })
        };

        var payload = JsonSerializer.Serialize(requestBody, JsonOptions);
        using var request = new HttpRequestMessage(HttpMethod.Post, OpenAiConstants.ChatCompletionsEndpoint)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var rawResponse = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("OpenAI API request failed with status {StatusCode}: {Response}", (int)response.StatusCode, rawResponse);
            throw new InvalidOperationException($"OpenAI API request failed with status {(int)response.StatusCode}.");
        }

        using var jsonDocument = JsonDocument.Parse(rawResponse);

        var root = jsonDocument.RootElement;
        var responseModel = root.TryGetProperty("model", out var modelJson) ? modelJson.GetString() ?? model : model;
        var assistantContent = ExtractAssistantContent(root);

        if (string.IsNullOrWhiteSpace(assistantContent))
        {
            throw new InvalidOperationException("OpenAI returned an empty response.");
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

        var prompt = usageJson.TryGetProperty("prompt_tokens", out var promptJson) ? promptJson.GetInt32() : 0;
        var completion = usageJson.TryGetProperty("completion_tokens", out var completionJson) ? completionJson.GetInt32() : 0;
        var total = usageJson.TryGetProperty("total_tokens", out var totalJson) ? totalJson.GetInt32() : prompt + completion;

        return new AssistantUsage(prompt, completion, total);
    }

    private static string ExtractAssistantContent(JsonElement root)
    {
        if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("OpenAI response does not contain choices.");
        }

        var firstChoice = choices[0];
        if (!firstChoice.TryGetProperty("message", out var message))
        {
            throw new InvalidOperationException("OpenAI response does not contain message payload.");
        }

        if (!message.TryGetProperty("content", out var content))
        {
            throw new InvalidOperationException("OpenAI response does not contain message content.");
        }

        return content.ValueKind switch
        {
            JsonValueKind.String => content.GetString() ?? string.Empty,
            JsonValueKind.Array => string.Join(string.Empty, content.EnumerateArray()
                .Where(x => x.TryGetProperty("type", out var type) && type.GetString() == "text")
                .Select(x => x.TryGetProperty("text", out var text) ? text.GetString() : string.Empty)
                .Where(x => !string.IsNullOrEmpty(x))),
            _ => string.Empty
        };
    }

    private static string ToApiRole(MessageRole role)
    {
        return role switch
        {
            MessageRole.System => "system",
            MessageRole.User => "user",
            MessageRole.Assistant => "assistant",
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unsupported role.")
        };
    }
}
