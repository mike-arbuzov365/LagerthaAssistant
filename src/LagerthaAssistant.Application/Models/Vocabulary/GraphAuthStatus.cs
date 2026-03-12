namespace LagerthaAssistant.Application.Models.Vocabulary;

public sealed record GraphAuthStatus(
    bool IsConfigured,
    bool IsAuthenticated,
    string Message,
    DateTimeOffset? AccessTokenExpiresAtUtc = null);
