namespace SharedBotKernel.Options;

public sealed class TelegramOptions
{
    public const string SectionName = "Telegram";

    public bool Enabled { get; set; } = false;

    public string BotToken { get; set; } = string.Empty;

    public string ApiBaseUrl { get; set; } = "https://api.telegram.org";

    public string? WebhookSecret { get; set; }

    public string? MiniAppSettingsUrl { get; set; }

    public int TextLengthLimit { get; set; } = 3900;
}
