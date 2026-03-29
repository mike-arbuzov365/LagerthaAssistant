namespace LagerthaAssistant.Domain.Entities;

public sealed class UserAiCredential
{
    public string Channel { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public string Provider { get; set; } = string.Empty;

    public string EncryptedApiKey { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
