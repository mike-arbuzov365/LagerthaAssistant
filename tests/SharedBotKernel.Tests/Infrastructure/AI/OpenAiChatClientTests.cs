namespace SharedBotKernel.Tests.Infrastructure.AI;

using System.Net;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using SharedBotKernel.Infrastructure.AI;
using SharedBotKernel.Options;
using Xunit;

public sealed class OpenAiChatClientTests
{
    private static readonly IReadOnlyCollection<ConversationMessage> SampleMessages =
    [
        ConversationMessage.Create(MessageRole.System, "System prompt", DateTimeOffset.UtcNow),
        ConversationMessage.Create(MessageRole.User, "Hello", DateTimeOffset.UtcNow)
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
                         "model": "gpt-4.1-mini",
                         "choices": [
                           { "message": { "content": "  Привіт!  " } }
                         ],
                         "usage": {
                           "prompt_tokens": 11,
                           "completion_tokens": 7,
                           "total_tokens": 18
                         }
                       }
                       """;

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json)
            };
        });

        var result = await sut.CompleteAsync(SampleMessages, "gpt-4.1-mini", "api-key");

        Assert.Equal("Привіт!", result.Content);
        Assert.Equal("gpt-4.1-mini", result.Model);
        Assert.NotNull(result.Usage);
        Assert.Equal(11, result.Usage!.PromptTokens);
        Assert.Equal(7, result.Usage.CompletionTokens);
        Assert.Equal(18, result.Usage.TotalTokens);

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest!.Method);
        Assert.Equal("https://api.openai.test/v1/chat/completions", capturedRequest.RequestUri!.ToString());
        Assert.Equal("Bearer", capturedRequest.Headers.Authorization?.Scheme);
        Assert.Equal("api-key", capturedRequest.Headers.Authorization?.Parameter);

        using var requestJson = JsonDocument.Parse(capturedBody!);
        Assert.Equal("gpt-4.1-mini", requestJson.RootElement.GetProperty("model").GetString());
    }

    [Fact]
    public async Task CompleteAsync_ShouldConcatenateTextChunks_WhenContentIsArray()
    {
        var sut = CreateSut((_, _) =>
        {
            var json = """
                       {
                         "choices": [
                           {
                             "message": {
                               "content": [
                                 { "type": "text", "text": "Перша " },
                                 { "type": "image_url", "image_url": "skip" },
                                 { "type": "text", "text": "частина" }
                               ]
                             }
                           }
                         ]
                       }
                       """;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json)
            });
        });

        var result = await sut.CompleteAsync(SampleMessages, "gpt-4.1-mini", "api-key");

        Assert.Equal("Перша частина", result.Content);
    }

    [Fact]
    public async Task CompleteAsync_ShouldThrowInvalidOperationException_WhenApiReturnsErrorStatus()
    {
        var sut = CreateSut((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{\"error\":\"bad request\"}")
        }));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.CompleteAsync(SampleMessages, "gpt-4.1-mini", "api-key"));

        Assert.Contains("failed with status 400", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CompleteAsync_ShouldThrowInvalidOperationException_WhenChoicesMissing()
    {
        var sut = CreateSut((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"id\":\"resp-1\"}")
        }));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.CompleteAsync(SampleMessages, "gpt-4.1-mini", "api-key"));

        Assert.Contains("does not contain choices", ex.Message, StringComparison.Ordinal);
    }

    private static OpenAiChatClient CreateSut(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> callback)
    {
        var handler = new CallbackHttpMessageHandler(callback);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.openai.test/v1/")
        };

        return new OpenAiChatClient(
            new OpenAiOptions
            {
                BaseUrl = "https://api.openai.test/v1/",
                Temperature = 0.2
            },
            NullLogger<OpenAiChatClient>.Instance,
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
