namespace SharedBotKernel.Tests.Infrastructure.Telegram;

using System.Net;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SharedBotKernel.Abstractions;
using SharedBotKernel.Infrastructure.Telegram;
using SharedBotKernel.Options;
using Xunit;

public sealed class TelegramBotSenderTests
{
    [Fact]
    public async Task SendTextAsync_ShouldReturnFailure_WhenIntegrationDisabled()
    {
        var sut = CreateSut(
            new TelegramOptions { Enabled = false, BotToken = "token" },
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        var result = await sut.SendTextAsync(42, "hello");

        Assert.False(result.Succeeded);
        Assert.Equal("Telegram integration is disabled.", result.ErrorMessage);
    }

    [Fact]
    public async Task SendTextAsync_ShouldReturnFailure_WhenBotTokenMissing()
    {
        var sut = CreateSut(
            new TelegramOptions { Enabled = true, BotToken = "  " },
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        var result = await sut.SendTextAsync(42, "hello");

        Assert.False(result.Succeeded);
        Assert.Equal("Telegram bot token is not configured.", result.ErrorMessage);
    }

    [Fact]
    public async Task SendTextAsync_ShouldReturnFailure_WhenMessageTextEmpty()
    {
        var sut = CreateSut(
            new TelegramOptions { Enabled = true, BotToken = "token" },
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        var result = await sut.SendTextAsync(42, "   ");

        Assert.False(result.Succeeded);
        Assert.Equal("Telegram message text is empty.", result.ErrorMessage);
    }

    [Fact]
    public async Task SendTextAsync_ShouldSendJsonPayloadAndBuildExpectedUrl_WhenRequestValid()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;
        var sut = CreateSut(
            new TelegramOptions
            {
                Enabled = true,
                BotToken = "bot-token",
                ApiBaseUrl = "https://api.telegram.org/"
            },
            async (request, ct) =>
            {
                capturedRequest = request;
                capturedBody = await request.Content!.ReadAsStringAsync(ct);
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

        var result = await sut.SendTextAsync(
            chatId: 1001,
            text: "Hello",
            options: new TelegramSendOptions(ParseMode: "MarkdownV2"),
            messageThreadId: 77);

        Assert.True(result.Succeeded);
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest!.Method);
        Assert.Equal("https://api.telegram.org/botbot-token/sendMessage", capturedRequest.RequestUri!.ToString());

        using var document = JsonDocument.Parse(capturedBody!);
        var root = document.RootElement;
        Assert.Equal(1001, root.GetProperty("chat_id").GetInt64());
        Assert.Equal("Hello", root.GetProperty("text").GetString());
        Assert.Equal(77, root.GetProperty("message_thread_id").GetInt32());
        Assert.Equal("MarkdownV2", root.GetProperty("parse_mode").GetString());
    }

    [Fact]
    public async Task SendTextAsync_ShouldReturnStatusAndBody_WhenApiReturnsError()
    {
        var sut = CreateSut(
            new TelegramOptions { Enabled = true, BotToken = "token" },
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{\"ok\":false,\"description\":\"bad request\"}")
            }));

        var result = await sut.SendTextAsync(42, "hello");

        Assert.False(result.Succeeded);
        Assert.Equal((int)HttpStatusCode.BadRequest, result.HttpStatusCode);
        Assert.Contains("bad request", result.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnswerCallbackQueryAsync_ShouldTrimCallbackId_WhenSendingPayload()
    {
        string? capturedBody = null;
        var sut = CreateSut(
            new TelegramOptions { Enabled = true, BotToken = "token" },
            async (request, ct) =>
            {
                capturedBody = await request.Content!.ReadAsStringAsync(ct);
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

        var result = await sut.AnswerCallbackQueryAsync("  cbq-123  ", "done");

        Assert.True(result.Succeeded);
        using var document = JsonDocument.Parse(capturedBody!);
        Assert.Equal("cbq-123", document.RootElement.GetProperty("callback_query_id").GetString());
        Assert.Equal("done", document.RootElement.GetProperty("text").GetString());
    }

    [Fact]
    public async Task SendTextAsync_ShouldReturnFailure_WhenHttpRequestThrows()
    {
        var sut = CreateSut(
            new TelegramOptions { Enabled = true, BotToken = "token" },
            (_, _) => throw new HttpRequestException("boom"));

        var result = await sut.SendTextAsync(42, "hello");

        Assert.False(result.Succeeded);
        Assert.Contains("HTTP error", result.ErrorMessage ?? string.Empty, StringComparison.Ordinal);
    }

    private static TelegramBotSender CreateSut(
        TelegramOptions options,
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> callback)
    {
        var handler = new CallbackHttpMessageHandler(callback);
        var httpClientFactory = new FakeHttpClientFactory(handler);

        return new TelegramBotSender(
            Options.Create(options),
            httpClientFactory);
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

    private sealed class FakeHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;

        public FakeHttpClientFactory(HttpMessageHandler handler)
        {
            _handler = handler;
        }

        public HttpClient CreateClient(string name)
        {
            return new HttpClient(_handler, disposeHandler: false);
        }
    }
}
