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
    private readonly IHttpClientFactory? _httpClientFactory;
    private readonly HttpClient? _directClient;
    private readonly bool _ownsDirectClient;

    public TelegramBotSender(IOptions<TelegramOptions> options, IHttpClientFactory httpClientFactory)
    {
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
    }

    internal TelegramBotSender(TelegramOptions options, HttpClient? httpClient)
    {
        _options = options;

        if (httpClient is null)
        {
            _directClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            _ownsDirectClient = true;
        }
        else
        {
            _directClient = httpClient;
        }
    }

    public async Task<TelegramSendResult> SendTextAsync(
        long chatId,
        string text,
        TelegramSendOptions? options = null,
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

        var payload = new TelegramSendMessagePayload(
            chatId,
            text,
            messageThreadId,
            options?.ParseMode,
            options?.ReplyMarkup);
        var url = BuildSendMessageUrl(_options.ApiBaseUrl, _options.BotToken);

        var json = JsonSerializer.Serialize(payload);
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        try
        {
            if (_httpClientFactory is not null)
            {
                using var factoryClient = _httpClientFactory.CreateClient("telegram");
                return await ExecuteSendAsync(factoryClient, request, cancellationToken);
            }

            return await ExecuteSendAsync(_directClient!, request, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return new TelegramSendResult(false, "Request timed out or was cancelled.");
        }
        catch (HttpRequestException ex)
        {
            return new TelegramSendResult(false, $"HTTP error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return new TelegramSendResult(false, $"Unexpected error ({ex.GetType().Name}): {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_ownsDirectClient)
        {
            _directClient?.Dispose();
        }
    }

    private static async Task<TelegramSendResult> ExecuteSendAsync(
        HttpClient client,
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        using var response = await client.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return new TelegramSendResult(true, HttpStatusCode: (int)response.StatusCode);
        }

        var error = await response.Content.ReadAsStringAsync(cancellationToken);
        return new TelegramSendResult(false, error, (int)response.StatusCode);
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
        [property: System.Text.Json.Serialization.JsonPropertyName("message_thread_id")] int? MessageThreadId,
        [property: System.Text.Json.Serialization.JsonPropertyName("parse_mode")] string? ParseMode,
        [property: System.Text.Json.Serialization.JsonPropertyName("reply_markup")] object? ReplyMarkup);
}
