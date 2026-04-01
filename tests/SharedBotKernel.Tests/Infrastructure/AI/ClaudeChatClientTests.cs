namespace SharedBotKernel.Tests.Infrastructure.AI;

using System.Net;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using SharedBotKernel.Constants;
using SharedBotKernel.Infrastructure.AI;
using SharedBotKernel.Options;
using Xunit;

public sealed class ClaudeChatClientTests
{
    private static readonly IReadOnlyCollection<ConversationMessage> SampleMessages =
    [
        ConversationMessage.Create(MessageRole.System, "System prompt", DateTimeOffset.UtcNow),
        ConversationMessage.Create(MessageRole.User, "Hello", DateTimeOffset.UtcNow),
        ConversationMessage.Create(MessageRole.Assistant, "Hi", DateTimeOffset.UtcNow)
    ];

    [Fact]
    public async Task CompleteAsync_ShouldReturnParsedResultAndUsage_WhenResponseIsValid()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;

        var sut = CreateSut(async (request, ct) =>
        {
            capturedRequest = request;
            capturedBody = await request.Content!.ReadAsStringAsync(ct);

            var json = """
                       {
                         "model": "claude-3-5-haiku-latest",
                         "content": [
                           { "type": "text", "text": "  Вітаю " },
                           { "type": "text", "text": "світ  " }
                         ],
                         "usage": {
                           "input_tokens": 21,
                           "output_tokens": 9
                         }
                       }
                       """;

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json)
            };
        });

        var result = await sut.CompleteAsync(SampleMessages, "claude-3-5-haiku-latest", "api-key");

        Assert.Equal("Вітаю світ", result.Content);
        Assert.Equal("claude-3-5-haiku-latest", result.Model);
        Assert.NotNull(result.Usage);
        Assert.Equal(21, result.Usage!.PromptTokens);
        Assert.Equal(9, result.Usage.CompletionTokens);
        Assert.Equal(30, result.Usage.TotalTokens);

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest!.Method);
        Assert.Equal("https://api.anthropic.test/v1/messages", capturedRequest.RequestUri!.ToString());
        Assert.Equal("api-key", capturedRequest.Headers.GetValues("x-api-key").Single());
        Assert.Equal(ClaudeConstants.AnthropicVersion, capturedRequest.Headers.GetValues(ClaudeConstants.AnthropicVersionHeader).Single());

        using var requestJson = JsonDocument.Parse(capturedBody!);
        Assert.Equal("claude-3-5-haiku-latest", requestJson.RootElement.GetProperty("model").GetString());
        Assert.Equal("System prompt", requestJson.RootElement.GetProperty("system").GetString());
        Assert.Equal(2, requestJson.RootElement.GetProperty("messages").GetArrayLength());
    }

    [Fact]
    public async Task CompleteAsync_ShouldUseRequestedModel_WhenResponseModelMissing()
    {
        var sut = CreateSut((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                                       {
                                         "content": [
                                           { "type": "text", "text": "ok" }
                                         ]
                                       }
                                       """)
        }));

        var result = await sut.CompleteAsync(SampleMessages, "claude-custom-model", "api-key");

        Assert.Equal("claude-custom-model", result.Model);
    }

    [Fact]
    public async Task CompleteAsync_ShouldThrowInvalidOperationException_WhenApiReturnsErrorStatus()
    {
        var sut = CreateSut((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("{\"error\":\"unauthorized\"}")
        }));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.CompleteAsync(SampleMessages, "claude-3-5-haiku-latest", "api-key"));

        Assert.Contains("failed with status 401", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CompleteAsync_ShouldThrowInvalidOperationException_WhenContentIsMissing()
    {
        var sut = CreateSut((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"id\":\"resp-1\"}")
        }));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.CompleteAsync(SampleMessages, "claude-3-5-haiku-latest", "api-key"));

        Assert.Contains("returned an empty response", ex.Message, StringComparison.Ordinal);
    }

    private static ClaudeChatClient CreateSut(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> callback)
    {
        var handler = new CallbackHttpMessageHandler(callback);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.anthropic.test/v1/")
        };

        return new ClaudeChatClient(
            new ClaudeOptions
            {
                BaseUrl = "https://api.anthropic.test/v1/",
                MaxTokens = 1200,
                Temperature = 0.2
            },
            NullLogger<ClaudeChatClient>.Instance,
            httpClient);
    }

    private sealed class CallbackHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _callback;

        public CallbackHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> callback)
        {
            _callback = callback;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _callback(request, cancellationToken);
        }
    }
}
