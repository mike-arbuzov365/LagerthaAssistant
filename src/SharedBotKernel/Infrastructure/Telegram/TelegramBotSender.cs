namespace SharedBotKernel.Infrastructure.Telegram;

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using SharedBotKernel.Options;

public sealed class TelegramBotSender : ITelegramBotSender, IDisposable
{
    private static readonly JsonSerializerOptions TelegramJsonSerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

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
            return new TelegramSendResult(false, "Telegram integration is disabled.");

        if (string.IsNullOrWhiteSpace(_options.BotToken))
            return new TelegramSendResult(false, "Telegram bot token is not configured.");

        if (string.IsNullOrWhiteSpace(text))
            return new TelegramSendResult(false, "Telegram message text is empty.");

        var payload = new TelegramSendMessagePayload(
            chatId, text, messageThreadId, options?.ParseMode, options?.ReplyMarkup);
        var url = BuildUrl(_options.ApiBaseUrl, _options.BotToken, "sendMessage");

        return await PostAsync(url, payload, cancellationToken);
    }

    public async Task<TelegramSendResult> AnswerCallbackQueryAsync(
        string callbackQueryId,
        string? text = null,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return new TelegramSendResult(false, "Telegram integration is disabled.");

        if (string.IsNullOrWhiteSpace(_options.BotToken))
            return new TelegramSendResult(false, "Telegram bot token is not configured.");

        if (string.IsNullOrWhiteSpace(callbackQueryId))
            return new TelegramSendResult(false, "Callback query id is empty.");

        var payload = new TelegramAnswerCallbackPayload(callbackQueryId.Trim(), text);
        var url = BuildUrl(_options.ApiBaseUrl, _options.BotToken, "answerCallbackQuery");

        return await PostAsync(url, payload, cancellationToken);
    }

    public void Dispose()
    {
        if (_ownsDirectClient)
            _directClient?.Dispose();
    }

    private async Task<TelegramSendResult> PostAsync<T>(
        string url,
        T payload,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload, TelegramJsonSerializerOptions);
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

    private static async Task<TelegramSendResult> ExecuteSendAsync(
        HttpClient client,
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        using var response = await client.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
            return new TelegramSendResult(true, HttpStatusCode: (int)response.StatusCode);

        var error = await response.Content.ReadAsStringAsync(cancellationToken);
        return new TelegramSendResult(false, error, (int)response.StatusCode);
    }

    private static string BuildUrl(string apiBaseUrl, string botToken, string method)
    {
        var baseUrl = string.IsNullOrWhiteSpace(apiBaseUrl)
            ? "https://api.telegram.org"
            : apiBaseUrl.Trim().TrimEnd('/');

        return $"{baseUrl}/bot{botToken}/{method}";
    }

    private sealed record TelegramSendMessagePayload(
        [property: JsonPropertyName("chat_id")] long ChatId,
        [property: JsonPropertyName("text")] string Text,
        [property: JsonPropertyName("message_thread_id")] int? MessageThreadId,
        [property: JsonPropertyName("parse_mode")] string? ParseMode,
        [property: JsonPropertyName("reply_markup")] object? ReplyMarkup);

    private sealed record TelegramAnswerCallbackPayload(
        [property: JsonPropertyName("callback_query_id")] string CallbackQueryId,
        [property: JsonPropertyName("text")] string? Text);
}
