namespace SharedBotKernel.Domain.Entities;

public sealed class GraphAuthToken
{
    public string Provider { get; set; } = string.Empty;

    public string AccessToken { get; set; } = string.Empty;

    public string RefreshToken { get; set; } = string.Empty;

    public DateTimeOffset AccessTokenExpiresAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
