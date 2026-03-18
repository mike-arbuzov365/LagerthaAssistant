namespace LagerthaAssistant.IntegrationTests.Services;

using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using LagerthaAssistant.Api.Interfaces;
using LagerthaAssistant.Api.Options;
using LagerthaAssistant.Api.Services;
using Microsoft.Extensions.Options;
using Xunit;

public sealed class TelegramBotSenderTests
{
    [Fact]
    public async Task SendTextAsync_ShouldOmitParseMode_WhenNotProvided()
    {
        using var handler = new StubHttpMessageHandler(
        [
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"ok\":true}", Encoding.UTF8, "application/json")
            }
        ]);

        using var httpClient = new HttpClient(handler);
        var sut = new TelegramBotSender(
            Options.Create(new TelegramOptions
            {
                Enabled = true,
                BotToken = "test-token",
                ApiBaseUrl = "https://api.telegram.org"
            }),
            new StubHttpClientFactory(httpClient));

        var result = await sut.SendTextAsync(
            12345,
            "Hello from test",
            new TelegramSendOptions(ParseMode: null, ReplyMarkup: null),
            messageThreadId: null,
            cancellationToken: CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Single(handler.RequestBodies);

        using var json = JsonDocument.Parse(handler.RequestBodies[0]);
        Assert.False(json.RootElement.TryGetProperty("parse_mode", out _));
    }

    [Fact]
    public async Task SendTextAsync_ShouldIncludeParseMode_WhenProvided()
    {
        using var handler = new StubHttpMessageHandler(
        [
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"ok\":true}", Encoding.UTF8, "application/json")
            }
        ]);

        using var httpClient = new HttpClient(handler);
        var sut = new TelegramBotSender(
            Options.Create(new TelegramOptions
            {
                Enabled = true,
                BotToken = "test-token",
                ApiBaseUrl = "https://api.telegram.org"
            }),
            new StubHttpClientFactory(httpClient));

        var result = await sut.SendTextAsync(
            12345,
            "Hello from test",
            new TelegramSendOptions(ParseMode: "HTML", ReplyMarkup: null),
            messageThreadId: null,
            cancellationToken: CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Single(handler.RequestBodies);

        using var json = JsonDocument.Parse(handler.RequestBodies[0]);
        Assert.True(json.RootElement.TryGetProperty("parse_mode", out var parseMode));
        Assert.Equal("HTML", parseMode.GetString());
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public StubHttpClientFactory(HttpClient client)
        {
            _client = client;
        }

        public HttpClient CreateClient(string name) => _client;
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;

        public StubHttpMessageHandler(IEnumerable<HttpResponseMessage> responses)
        {
            _responses = new Queue<HttpResponseMessage>(responses);
        }

        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            RequestBodies.Add(body);

            if (_responses.Count == 0)
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("{\"ok\":false}", Encoding.UTF8, "application/json")
                };
            }

            return _responses.Dequeue();
        }
    }
}

