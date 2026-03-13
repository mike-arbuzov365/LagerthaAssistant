namespace LagerthaAssistant.Api.Options;

public sealed class TelegramOptions
{
    public bool Enabled { get; set; } = false;

    public string BotToken { get; set; } = string.Empty;

    public string ApiBaseUrl { get; set; } = "https://api.telegram.org";

    public string? WebhookSecret { get; set; }
}
