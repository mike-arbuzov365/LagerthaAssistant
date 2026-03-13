using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LagerthaAssistant.Api.Interfaces;
using LagerthaAssistant.Api.Options;
using Microsoft.Extensions.Options;

namespace LagerthaAssistant.Api.Services;

public sealed class TelegramBotSender : ITelegramBotSender, IDisposable
{
    private readonly TelegramOptions _options;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public TelegramBotSender(IOptions<TelegramOptions> options)
        : this(options.Value, null)
    {
    }

    internal TelegramBotSender(TelegramOptions options, HttpClient? httpClient)
    {
        _options = options;

        if (httpClient is null)
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            _ownsHttpClient = true;
        }
        else
        {
            _httpClient = httpClient;
            _ownsHttpClient = false;
        }
    }

    public async Task<TelegramSendResult> SendTextAsync(
        long chatId,
        string text,
        int? messageThreadId = null,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return new TelegramSendResult(false, "Telegram integration is disabled.");
        }

        if (string.IsNullOrWhiteSpace(_options.BotToken))
        {
            return new TelegramSendResult(false, "Telegram bot token is not configured.");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return new TelegramSendResult(false, "Telegram message text is empty.");
        }

        var payload = new TelegramSendMessagePayload(chatId, text, messageThreadId);
        var url = BuildSendMessageUrl(_options.ApiBaseUrl, _options.BotToken);

        var json = JsonSerializer.Serialize(payload);
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return new TelegramSendResult(true, HttpStatusCode: (int)response.StatusCode);
            }

            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            return new TelegramSendResult(false, error, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            return new TelegramSendResult(false, ex.Message);
        }
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private static string BuildSendMessageUrl(string apiBaseUrl, string botToken)
    {
        var baseUrl = string.IsNullOrWhiteSpace(apiBaseUrl)
            ? "https://api.telegram.org"
            : apiBaseUrl.Trim().TrimEnd('/');

        return $"{baseUrl}/bot{botToken}/sendMessage";
    }

    private sealed record TelegramSendMessagePayload(
        [property: System.Text.Json.Serialization.JsonPropertyName("chat_id")] long ChatId,
        [property: System.Text.Json.Serialization.JsonPropertyName("text")] string Text,
        [property: System.Text.Json.Serialization.JsonPropertyName("message_thread_id")] int? MessageThreadId);
}
